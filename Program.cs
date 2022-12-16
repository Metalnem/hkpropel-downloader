using System.Xml.Linq;

namespace Hkpropel;

public class Program
{
    private const string MetaInf = "META-INF";
    private const string Oebps = "OEBPS";
    private const string ContainerName = "container.xml";
    private const string PackageName = "package.opf";

    public static async Task Main(string[] args)
    {
        var baseAddress = Environment.GetEnvironmentVariable("HKPROPEL_URL");
        var cookie = Environment.GetEnvironmentVariable("HKPROPEL_COOKIE");

        using var handler = new HttpClientHandler { UseCookies = false };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };

        using var response = await httpClient.GetAsync(PackageName);
        var package = await response.Content.ReadAsStringAsync();

        var outputDir = Path.GetRandomFileName();
        await CreateEpubMetadata(outputDir, package);

        using var reader = new StringReader(package);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
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
}
