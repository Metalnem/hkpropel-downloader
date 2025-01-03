﻿using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
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
        var bookId = new Option<string>("--id", "HK Propel book ID.") { IsRequired = true };
        var cookiesPath = new Option<string>("--cookies", "Path to exported Chrome cookies.") { IsRequired = true };
        var command = new RootCommand("Download HK Propel books in EPUB format.") { bookId, cookiesPath };

        command.SetHandler(Download, bookId, cookiesPath);
        return await command.InvokeAsync(args);
    }

    private static async Task<int> Download(string bookId, string cookiesPath)
    {
        var json = await File.ReadAllTextAsync(cookiesPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var cookies = JsonSerializer.Deserialize<CookieJson[]>(json, options)
            .Select(cookie => cookie.ToCookie())
            .Where(cookie => cookie.Domain.EndsWith(".humankinetics.com"))
            .ToList();

        if (!cookies.Any(cookie => cookie.Name == "jwt_token"))
        {
            Console.Error.WriteLine("HK Propel authentication cookie was not found.");
            return 1;
        }

        using var apiClient = new ApiClient(cookies);

        var bookInfo = await apiClient.GetBookInfo(bookId);
        var keyEncryptionKey = await apiClient.GetKeyEncryptionKey();
        var contentEncryptionKey = Crypto.DecryptKey(bookInfo.Key, keyEncryptionKey);

        var packageDocumentBytes = await apiClient.GetResource(bookInfo, PackageName);
        var packageDocument = Encoding.UTF8.GetString(packageDocumentBytes);

        var outputDir = Path.GetRandomFileName();
        await CreateEpubMetadata(outputDir, packageDocument);

        using var reader = new StringReader(packageDocument);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
        var resources = GetAllResources(document);

        await Parallel.ForEachAsync(resources, async (resource, cancellationToken) =>
        {
            var data = await apiClient.GetResource(bookInfo, resource);

            if (resource == "nav.xhtml")
            {
                data = Crypto.DecryptContent(
                    Encoding.UTF8.GetString(data),
                    Encoding.UTF8.GetString(contentEncryptionKey)
                );
            }

            var path = Path.Combine(outputDir, Oebps, resource);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            await File.WriteAllBytesAsync(path, data, cancellationToken);
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
