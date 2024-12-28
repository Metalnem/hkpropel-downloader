using System.Data.SQLite;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Hkpropel;

public class ChromeCookieManager
{
    private readonly string path;
    private readonly string password;

    public ChromeCookieManager()
    {
        path = GetCookiesDatabasePath();

        if (path is null)
        {
            throw new Exception("Chrome cookies database not found.");
        }

        password = GetChromeSafeStoragePassword();

        if (string.IsNullOrEmpty(password))
        {
            throw new Exception("Failed to retrieve Chrome Safe Storage password.");
        }
    }

    public IEnumerable<Cookie> GetCookiesMac()
    {
        using var connection = new SQLiteConnection($"Data Source={path};Version=3;");
        connection.Open();

        var query = "SELECT host_key, name, encrypted_value FROM cookies";

        using var command = new SQLiteCommand(query, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var host = reader.GetString(0);
            var name = reader.GetString(1);
            var value = DecryptCookieValue((byte[])reader["encrypted_value"]);
            var cookie = new Cookie { Domain = host, Value = value };

            if (!string.IsNullOrEmpty(name))
            {
                cookie.Name = name;
            }

            yield return cookie;
        }
    }

    private static string GetCookiesDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var chromePath = Path.Combine(appDataPath, "Google/Chrome");

        if (!Directory.Exists(chromePath))
        {
            throw new Exception("Chrome is not installed on this machine.");
        }

        return Directory.GetDirectories(chromePath, "Profile *")
            .SelectMany(profile => Directory.GetFiles(profile, "Cookies"))
            .MaxBy(File.GetLastWriteTimeUtc);
    }

    private static string GetChromeSafeStoragePassword()
    {
        var arguments = "find-generic-password -w -s \"Chrome Safe Storage\"";

        var startInfo = new ProcessStartInfo("security", arguments)
        {
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output.Trim();
    }

    private string DecryptCookieValue(byte[] encryptedValue)
    {
        if (encryptedValue == null || encryptedValue.Length == 0)
        {
            return string.Empty;
        }

        var prefix = Encoding.UTF8.GetBytes("v10");

        if (!encryptedValue.AsSpan().StartsWith(prefix))
        {
            throw new Exception("Encrypted cookie value is missing the required v10 prefix.");
        }

        using var aes = Aes.Create();

        aes.Key = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: Encoding.ASCII.GetBytes("saltysalt"),
            iterations: 1003,
            hashAlgorithm: HashAlgorithmName.SHA1,
            outputLength: 16
        );

        var plaintext = aes.DecryptCbc(
            ciphertext: encryptedValue.AsSpan(prefix.Length),
            iv: Encoding.ASCII.GetBytes(new string(' ', 16)),
            paddingMode: PaddingMode.PKCS7
        );

        return Encoding.UTF8.GetString(plaintext);
    }
}
