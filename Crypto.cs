using System.Security.Cryptography;

namespace Hkpropel;

public static class Crypto
{
    public static byte[] DecryptKey(string ciphertext, string key)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(key);

        return aes.DecryptEcb(Convert.FromBase64String(ciphertext), PaddingMode.PKCS7);
    }

    public static byte[] DecryptContent(string ciphertext, string key)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString(key);

        return aes.DecryptCbc(Convert.FromHexString(ciphertext), new byte[16], PaddingMode.PKCS7);
    }
}
