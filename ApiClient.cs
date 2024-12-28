using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hkpropel;

public class ApiClient : IDisposable
{
    private readonly HttpClientHandler handler;
    private readonly HttpClient client;

    public ApiClient(IEnumerable<Cookie> cookies)
    {
        handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        client = new HttpClient(handler) { BaseAddress = new Uri("https://hkpropel.humankinetics.com/") };

        foreach (var cookie in cookies)
        {
            handler.CookieContainer.Add(cookie);
        }
    }

    public async Task<BookInfo> GetBookInfo(string id)
    {
        using var response = await client.GetAsync($"ebookreader/launchbook.htm?id={id}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var passKey = GetMagicSettingsVariable(content, "passKey");
        var epubContentPath = GetMagicSettingsVariable(content, "epubContentPath");
        var contentUpdated = GetMagicSettingsVariable(content, "contentUpdated");

        return new BookInfo($"{epubContentPath}/{contentUpdated}/OPS", passKey);
    }

    public async Task<string> GetKeyEncryptionKey()
    {
        using var response = await client.GetAsync("services/configuration/getConfigReader.json");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);

        return json.RootElement.GetProperty("object").GetProperty("dummyText").GetString();
    }

    public async Task<byte[]> GetResource(BookInfo bookInfo, string resource)
    {
        using var response = await client.GetAsync($"{bookInfo.Url}/{resource}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string GetMagicSettingsVariable(string content, string variable)
    {
        var pattern = $"magicSettings.{variable} = \"(.+?)\"";
        var match = Regex.Match(content, pattern);

        return match.Groups[1].Value;
    }

    public void Dispose()
    {
        handler.Dispose();
        client.Dispose();
    }
}
