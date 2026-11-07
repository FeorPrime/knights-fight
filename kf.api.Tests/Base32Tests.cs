using System.Text;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using Xunit;

namespace KnightFight.Api.Tests;

public class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY======")]
    [InlineData("fo", "MZXQ====")]
    [InlineData("foo", "MZXW6===")]
    [InlineData("foob", "MZXW6YQ=")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI======")]
    public void ToBase32String_WithPadding_MatchesRfc4648TestVectors(string input, string expected)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(input);
        string result = Base32.ToBase32String(bytes, pad: true);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void ToBase32String_WithoutPadding_MatchesExpected(string input, string expected)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(input);
        string result = Base32.ToBase32String(bytes, pad: false);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("MY======", "f")]
    [InlineData("MY", "f")]
    [InlineData("MZXQ====", "fo")]
    [InlineData("MZXQ", "fo")]
    [InlineData("MZXW6===", "foo")]
    [InlineData("MZXW6", "foo")]
    [InlineData("MZXW6YQ=", "foob")]
    [InlineData("MZXW6YQ", "foob")]
    [InlineData("MZXW6YTB", "fooba")]
    [InlineData("MZXW6YTBOI======", "foobar")]
    [InlineData("MZXW6YTBOI", "foobar")]
    public void FromBase32String_DecodesCorrectly(string encoded, string expected)
    {
        byte[] bytes = Base32.FromBase32String(encoded);
        string decoded = Encoding.ASCII.GetString(bytes);
        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void FromBase32String_IsCaseInsensitive()
    {
        byte[] upper = Base32.FromBase32String("MZXW6YTBOI");
        byte[] lower = Base32.FromBase32String("mzxw6ytboi");
        byte[] mixed = Base32.FromBase32String("mZxW6yTbOi");

        Assert.Equal(upper, lower);
        Assert.Equal(upper, mixed);
        Assert.Equal("foobar", Encoding.ASCII.GetString(lower));
    }

    [Fact]
    public void FromBase32String_IgnoresSpacesAndDashes()
    {
        byte[] normal = Base32.FromBase32String("MZXW6YTBOI");
        byte[] withSpaces = Base32.FromBase32String("MZXW 6YT BOI");
        byte[] withDashes = Base32.FromBase32String("MZXW-6YT-BOI");

        Assert.Equal(normal, withSpaces);
        Assert.Equal(normal, withDashes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(100)]
    public void Roundtrip_RandomBytes_PreservesData(int length)
    {
        byte[] original = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(original);

        string encoded = Base32.ToBase32String(original);
        byte[] decoded = Base32.FromBase32String(encoded);

        Assert.Equal(original, decoded);
    }

    [Theory]
    [InlineData("INVALID1890")]
    [InlineData("1234567890")]
    [InlineData("MZXW6YTBO!")]
    public void TryFromBase32String_InvalidChars_ReturnsFalse(string invalidInput)
    {
        bool success = Base32.TryFromBase32String(invalidInput, out var result);
        Assert.False(success);
        Assert.Empty(result);
    }

    [Fact]
    public void FromBase32String_InvalidChars_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Base32.FromBase32String("INVALID1890"));
    }
}
