using System.Text;

namespace KnightFight.Api.Infrastructure.Services.Crypto;

/// <summary>
/// Provides utility methods for encoding and decoding data using the Base32 encoding scheme.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string ToBase32String(byte[] data, bool pad = false)
    {
        if (data == null || data.Length == 0) return string.Empty;

        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                int index = (buffer >> bitsLeft) & 0x1F;
                result.Append(Alphabet[index]);
            }
        }

        if (bitsLeft > 0)
        {
            int index = (buffer << (5 - bitsLeft)) & 0x1F;
            result.Append(Alphabet[index]);
        }

        if (pad)
        {
            while (result.Length % 8 != 0)
            {
                result.Append('=');
            }
        }

        return result.ToString();
    }

    public static bool TryFromBase32String(string? input, out byte[] result)
    {
        result = [];
        if (input == null) return false;
        if (input.Length == 0) return true;

        string cleanInput = input.Trim().TrimEnd('=').Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (cleanInput.Length == 0) return true;

        var output = new List<byte>(cleanInput.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in cleanInput)
        {
            int val;
            if (c >= 'A' && c <= 'Z')
                val = c - 'A';
            else if (c >= '2' && c <= '7')
                val = c - '2' + 26;
            else
                return false;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        result = output.ToArray();
        return true;
    }

    public static byte[] FromBase32String(string input)
    {
        if (TryFromBase32String(input, out var result))
        {
            return result;
        }
        throw new ArgumentException("Invalid Base32 string.", nameof(input));
    }
}
