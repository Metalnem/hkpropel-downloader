using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Hkpropel;

public class Program
{
    private const string MetaInf = "META-INF";
    private const string Oebps = "OEBPS";
    private const string ContainerName = "container.xml";
    private const string PackageName = "package.opf";

    public static async Task<int> Main(string[] args)
    {
        var baseAddress = Environment.GetEnvironmentVariable("HKPROPEL_URL");
        var cookie = Environment.GetEnvironmentVariable("HKPROPEL_COOKIE");

        if (baseAddress is null || cookie is null)
        {
            Console.Error.WriteLine("HKPROPEL_URL or HKPROPEL_COOKIE are missing.");
            return 1;
        }

        using var handler = new HttpClientHandler { UseCookies = false };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        httpClient.DefaultRequestHeaders.Add("Cookie", cookie);

        using var response = await httpClient.GetAsync(PackageName);
        response.EnsureSuccessStatusCode();

        var package = await response.Content.ReadAsStringAsync();
        var outputDir = Path.GetRandomFileName();
        await CreateEpubMetadata(outputDir, package);

        using var reader = new StringReader(package);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
        var resources = GetAllResources(document);

        await Parallel.ForEachAsync(resources, async (resource, _) =>
        {
            await DownloadResource(httpClient, outputDir, resource);
        });

        string bookName = GetBookName(document);
        ZipFile.CreateFromDirectory(outputDir, $"{bookName}.epub");
        Directory.Delete(outputDir, true);

        return 0;
    }

    private static async Task CreateEpubMetadata(string path, string package)
    {
        Directory.CreateDirectory(Path.Combine(path, MetaInf));
        Directory.CreateDirectory(Path.Combine(path, Oebps));

        string container = await File.ReadAllTextAsync(ContainerName);

        await File.WriteAllTextAsync(Path.Combine(path, MetaInf, ContainerName), container);
        await File.WriteAllTextAsync(Path.Combine(path, "mimetype"), "application/epub+zip");
        await File.WriteAllTextAsync(Path.Combine(path, Oebps, PackageName), package);
    }

    private static HashSet<string> GetAllResources(XDocument document)
    {
        var references = new HashSet<string>();

        foreach (var element in document.Descendants())
        {
            var href = element.Attribute("href");

            if (href != null)
            {
                references.Add(href.Value);
            }
        }

        return references;
    }

    private static async Task DownloadResource(HttpClient httpClient, string outputDir, string resource)
    {
        using var response = await httpClient.GetAsync(resource);
        response.EnsureSuccessStatusCode();

        var path = Path.Combine(outputDir, Oebps, resource);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        var data = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(path, data);
    }

    private static string GetBookName(XDocument document)
    {
        XNamespace ns = "http://purl.org/dc/elements/1.1/";
        var title = document.Descendants(ns + "title").SingleOrDefault();

        if (title is null)
        {
            return "Book";
        }

        var sb = new StringBuilder();
        var invalid = Path.GetInvalidFileNameChars();

        foreach (var c in title.Value)
        {
            if (!invalid.Contains(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
