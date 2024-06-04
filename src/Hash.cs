using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal static class Hash
{
    public static string ComputeHash(string key, IDictionary<string, string> formValues)
    {
        string message = GetMacString(formValues);

        return ComputeHash(key, message);
    }


    public static string ComputeHash(string key, string message)
    {
        var encoding = new UTF8Encoding();
        byte[] byteKey = encoding.GetBytes(key);

        using (HMACSHA256 hmac = new HMACSHA256(byteKey))
        {
            var messageBytes = encoding.GetBytes(message);
            var hashedBytes = hmac.ComputeHash(messageBytes);

            return ByteArrayToHexString(hashedBytes);
        }
    }

    private static string ByteArrayToHexString(byte[] bytes)
    {
        var result = new StringBuilder();
        foreach (byte b in bytes)
        {
            result.Append(b.ToString("x2"));
        }

        return result.ToString();
    }

    private static string GetMacString(IDictionary<string, string> formValues)
    {
        var excludeList = new List<string> { "MAC" };
        var keysSorted = formValues.Keys.ToArray();
        Array.Sort(keysSorted, StringComparer.Ordinal);

        var message = new StringBuilder();
        foreach (string key in keysSorted)
        {
            if (excludeList.Contains(key))
                continue;

            if (message.Length > 0)
                message.Append(" ");

            var value = formValues[key];
            message.Append(value);
        }

        return message.ToString();
    }
}
