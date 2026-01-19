using System.Security.Cryptography;
using System.Text;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class Sha256IdGenerator : IIdGenerator
{
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int IdLength = 6;
    private const int BytesToUse = 5;

    public string Generate(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var normalized = title.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        return ToBase62(hash.AsSpan(0, BytesToUse));
    }

    private static string ToBase62(ReadOnlySpan<byte> bytes)
    {
        // Convert bytes to a large number, then to base62
        var result = new char[IdLength];
        var value = 0UL;

        foreach (var b in bytes)
        {
            value = (value << 8) | b;
        }

        for (var i = IdLength - 1; i >= 0; i--)
        {
            result[i] = Base62Chars[(int)(value % 62)];
            value /= 62;
        }

        return new string(result);
    }
}
