using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal static class Hash
{
    public static string ByteArrayToHexString(byte[] bytes)
    {
        var result = new StringBuilder();
        foreach (byte b in bytes)
        {
            result.Append(b.ToString("x2"));
        }

        return result.ToString();
    }

    public static string ComputeHash(string key, Stream message)
    {
        var encoding = new UTF8Encoding();
        var byteKey = encoding.GetBytes(key);

        using (HMACSHA256 hmac = new HMACSHA256(byteKey))
        {
            var hashedBytes = hmac.ComputeHash(message);
            return ByteArrayToHexString(hashedBytes);
        }
    }

    public static string ComputeHash(string key, string message)
    {
        var encoding = new UTF8Encoding();
        var byteKey = encoding.GetBytes(key);

        using (HMACSHA256 hmac = new HMACSHA256(byteKey))
        {
            var messageBytes = encoding.GetBytes(message);
            var hashedBytes = hmac.ComputeHash(messageBytes);

            return ByteArrayToHexString(hashedBytes);
        }
    }
}
