namespace Fleece.Core.FunctionalCore;

public static class IdGeneration
{
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int IdLength = 6;
    private const int BytesToUse = 5;

    public static string Generate()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return ToBase62(bytes.AsSpan(0, BytesToUse));
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
