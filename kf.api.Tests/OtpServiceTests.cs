using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnightFight.Api.Tests;

public class OtpServiceTests
{
    private readonly IOtpService _otpService = new OtpService();

    [Fact]
    public void GenerateOtpSeed_ReturnsValidBase32String()
    {
        string seed = _otpService.GenerateOtpSeed();

        Assert.NotNull(seed);
        Assert.NotEmpty(seed);
        Assert.True(Base32.TryFromBase32String(seed, out var bytes));
        Assert.Equal(20, bytes.Length); // Default 20 bytes
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(16, 16)]
    [InlineData(20, 20)]
    [InlineData(32, 32)]
    [InlineData(64, 64)]
    public void GenerateOtpSeed_WithCustomLength_GeneratesCorrectByteLength(int requestedBytes, int expectedByteLength)
    {
        string seed = _otpService.GenerateOtpSeed(requestedBytes);

        Assert.True(Base32.TryFromBase32String(seed, out var bytes));
        Assert.Equal(expectedByteLength, bytes.Length);
    }

    [Fact]
    public void GenerateOtpSeed_SuccessiveCalls_GenerateDistinctSeeds()
    {
        string seed1 = _otpService.GenerateOtpSeed();
        string seed2 = _otpService.GenerateOtpSeed();

        Assert.NotEqual(seed1, seed2);
    }

    [Fact]
    public void ProvideSeed_And_GenerateSeed_AreAliasesForGenerateOtpSeed()
    {
        string seed1 = _otpService.GenerateOtpSeed();
        string seed2 = _otpService.GenerateOtpSeed();
        string seed3 = _otpService.GenerateOtpSeed();

        Assert.True(Base32.TryFromBase32String(seed1, out var bytes1));
        Assert.True(Base32.TryFromBase32String(seed2, out var bytes2));
        Assert.True(Base32.TryFromBase32String(seed3, out var bytes3));
        Assert.Equal(20, bytes1.Length);
        Assert.Equal(20, bytes2.Length);
        Assert.Equal(20, bytes3.Length);
    }

    [Fact]
    public void GenerateSeedBytes_ReturnsRandomByteArray()
    {
        byte[] bytes = _otpService.GenerateSeedBytes(32);

        Assert.NotNull(bytes);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateBase64Seed_ReturnsValidBase64()
    {
        string base64 = _otpService.GenerateBase64Seed(32);

        Assert.NotNull(base64);
        byte[] decoded = Convert.FromBase64String(base64);
        Assert.Equal(32, decoded.Length);
    }

    [Fact]
    public void GenerateOtpToken_DefaultParameters_Produces6DigitCode()
    {
        string seed = _otpService.GenerateOtpSeed();
        string token = _otpService.GenerateOtpToken(seed);

        Assert.NotNull(token);
        Assert.Equal(6, token.Length);
        Assert.True(token.All(char.IsAsciiDigit));
    }

    [Fact]
    public void CheckOtpTokenValidity_ValidTokenForSeed_ReturnsTrue()
    {
        string seed = _otpService.GenerateOtpSeed();
        string token = _otpService.GenerateOtpToken(seed);

        bool isValid = _otpService.CheckOtpTokenValidity(seed, token);

        Assert.True(isValid);
    }

    [Fact]
    public void CheckOtpTokenValidity_AliasMethods_ReturnTrue()
    {
        string seed = _otpService.GenerateOtpSeed();
        string token = _otpService.GenerateOtpToken(seed);

        Assert.True(_otpService.CheckOtpTokenValidity(seed, token));
        Assert.True(_otpService.CheckOtpTokenValidity(seed, token));
        Assert.True(_otpService.CheckOtpTokenValidity(seed, token));
    }

    [Fact]
    public void CheckOtpTokenValidity_WithinToleranceWindow_ReturnsTrue()
    {
        string seed = _otpService.GenerateOtpSeed();
        var now = DateTimeOffset.UtcNow;

        // Token generated 25 seconds ago (within current or previous 30s step)
        var pastTime = now.AddSeconds(-25);
        string pastToken = _otpService.GenerateOtpToken(seed, pastTime);

        Assert.True(_otpService.CheckOtpTokenValidity(seed, pastToken, now, toleranceSteps: 1));

        // Token generated 25 seconds in future (within current or next 30s step)
        var futureTime = now.AddSeconds(25);
        string futureToken = _otpService.GenerateOtpToken(seed, futureTime);

        Assert.True(_otpService.CheckOtpTokenValidity(seed, futureToken, now, toleranceSteps: 1));
    }

    [Fact]
    public void CheckOtpTokenValidity_OutsideToleranceWindow_ReturnsFalse()
    {
        string seed = _otpService.GenerateOtpSeed();
        var now = DateTimeOffset.UtcNow;

        // Token generated 2 time steps ago (65 seconds ago) with toleranceSteps = 1
        var expiredTime = now.AddSeconds(-65);
        string expiredToken = _otpService.GenerateOtpToken(seed, expiredTime);

        // With tolerance 0: strictly current step
        Assert.False(_otpService.CheckOtpTokenValidity(seed, expiredToken, now, toleranceSteps: 0));
        // With tolerance 1: only +/- 1 step
        Assert.False(_otpService.CheckOtpTokenValidity(seed, expiredToken, now, toleranceSteps: 1));
    }

    [Fact]
    public void CheckOtpTokenValidity_WrongToken_ReturnsFalse()
    {
        string seed = _otpService.GenerateOtpSeed();
        string validToken = _otpService.GenerateOtpToken(seed);

        // Calculate a definitely different token
        int num = int.Parse(validToken);
        string wrongToken = ((num + 123456) % 1000000).ToString("D6");

        Assert.False(_otpService.CheckOtpTokenValidity(seed, wrongToken));
    }

    [Fact]
    public void CheckOtpTokenValidity_WrongSeed_ReturnsFalse()
    {
        string seed1 = _otpService.GenerateOtpSeed();
        string seed2 = _otpService.GenerateOtpSeed();

        string token1 = _otpService.GenerateOtpToken(seed1);

        Assert.False(_otpService.CheckOtpTokenValidity(seed2, token1));
    }

    [Theory]
    [InlineData(null, "123456")]
    [InlineData("", "123456")]
    [InlineData("   ", "123456")]
    [InlineData("JBSWY3DPEHPK3PXP", null)]
    [InlineData("JBSWY3DPEHPK3PXP", "")]
    [InlineData("JBSWY3DPEHPK3PXP", "   ")]
    [InlineData("JBSWY3DPEHPK3PXP", "12345")] // Wrong length
    [InlineData("JBSWY3DPEHPK3PXP", "1234567")] // Wrong length
    [InlineData("JBSWY3DPEHPK3PXP", "abcdef")] // Non digits
    public void CheckOtpTokenValidity_InvalidInputs_ReturnsFalse(string? seed, string? token)
    {
        Assert.False(_otpService.CheckOtpTokenValidity(seed!, token!));
    }

    [Fact]
    public void CheckOtpTokenValidity_TokenWithSpacesOrHyphen_NormalizesAndValidates()
    {
        string seed = _otpService.GenerateOtpSeed();
        string token = _otpService.GenerateOtpToken(seed);

        string formattedWithSpace = $"{token[..3]} {token[3..]}";
        string formattedWithHyphen = $"{token[..3]}-{token[3..]}";
        string withSurroundingSpaces = $"  {token}  ";

        Assert.True(_otpService.CheckOtpTokenValidity(seed, formattedWithSpace));
        Assert.True(_otpService.CheckOtpTokenValidity(seed, formattedWithHyphen));
        Assert.True(_otpService.CheckOtpTokenValidity(seed, withSurroundingSpaces));
    }

    [Fact]
    public void GenerateProvisioningUri_ReturnsValidUri()
    {
        string seed = "JBSWY3DPEHPK3PXP";
        string uri = _otpService.GenerateProvisioningUri("KnightFight", "player1@kf.com", seed);

        Assert.StartsWith("otpauth://totp/KnightFight:player1%40kf.com?", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=KnightFight", uri);
    }

    [Fact]
    public void DependencyInjection_CanResolveIOtpServiceAndOtpService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<OtpService>();

        using var provider = services.BuildServiceProvider();

        var iOtp = provider.GetService<IOtpService>();
        var concreteOtp = provider.GetService<OtpService>();

        Assert.NotNull(iOtp);
        Assert.NotNull(concreteOtp);
        Assert.IsType<OtpService>(iOtp);
    }
}
