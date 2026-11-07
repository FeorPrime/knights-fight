using System.Text;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using Xunit;

namespace KnightFight.Api.Tests;

public class OtpServiceRfcTests
{
    private readonly IOtpService _otpService = new OtpService();

    // RFC 4226 test secret: "12345678901234567890" (ASCII bytes)
    private static readonly byte[] RfcSecretBytes = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(0, "755224")]
    [InlineData(1, "287082")]
    [InlineData(2, "359152")]
    [InlineData(3, "969429")]
    [InlineData(4, "338314")]
    [InlineData(5, "254676")]
    [InlineData(6, "287922")]
    [InlineData(7, "162583")]
    [InlineData(8, "399871")]
    [InlineData(9, "520489")]
    public void GenerateHotp_MatchesRfc4226TestVectors(long counter, string expectedOtp)
    {
        string otp = _otpService.GenerateHotp(RfcSecretBytes, counter, digits: 6, OtpHashAlgorithm.Sha1);
        Assert.Equal(expectedOtp, otp);

        bool isValid = _otpService.ValidateHotp(RfcSecretBytes, expectedOtp, counter, digits: 6, OtpHashAlgorithm.Sha1);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(59L, "94287082", "287082")]
    [InlineData(1111111109L, "07081804", "081804")]
    [InlineData(1111111111L, "14050471", "050471")]
    [InlineData(1234567890L, "89005924", "005924")]
    [InlineData(2000000000L, "69279037", "279037")]
    [InlineData(20000000000L, "65353130", "353130")]
    public void GenerateTotp_Sha1_MatchesRfc6238TestVectors(long unixSeconds, string expected8Digits, string expected6Digits)
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        string otp8 = _otpService.GenerateOtpToken(RfcSecretBytes, timestamp, stepSeconds: 30, digits: 8, OtpHashAlgorithm.Sha1);
        Assert.Equal(expected8Digits, otp8);

        string otp6 = _otpService.GenerateOtpToken(RfcSecretBytes, timestamp, stepSeconds: 30, digits: 6, OtpHashAlgorithm.Sha1);
        Assert.Equal(expected6Digits, otp6);

        // Validation at exact time (toleranceSteps = 0)
        Assert.True(_otpService.CheckOtpTokenValidity(RfcSecretBytes, expected8Digits, timestamp, toleranceSteps: 0, stepSeconds: 30, digits: 8, OtpHashAlgorithm.Sha1));
        Assert.True(_otpService.CheckOtpTokenValidity(RfcSecretBytes, expected6Digits, timestamp, toleranceSteps: 0, stepSeconds: 30, digits: 6, OtpHashAlgorithm.Sha1));
    }

    // RFC 6238 SHA-256 test key: 32 bytes ASCII "12345678901234567890123456789012"
    private static readonly byte[] RfcSha256SecretBytes = Encoding.ASCII.GetBytes("12345678901234567890123456789012");

    [Theory]
    [InlineData(59L, "46119246")]
    [InlineData(1111111109L, "68084774")]
    [InlineData(1111111111L, "67062674")]
    [InlineData(1234567890L, "91819424")]
    [InlineData(2000000000L, "90698825")]
    [InlineData(20000000000L, "77737706")]
    public void GenerateTotp_Sha256_MatchesRfc6238TestVectors(long unixSeconds, string expected8Digits)
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        string otp = _otpService.GenerateOtpToken(RfcSha256SecretBytes, timestamp, stepSeconds: 30, digits: 8, OtpHashAlgorithm.Sha256);
        Assert.Equal(expected8Digits, otp);

        Assert.True(_otpService.CheckOtpTokenValidity(RfcSha256SecretBytes, expected8Digits, timestamp, toleranceSteps: 0, stepSeconds: 30, digits: 8, OtpHashAlgorithm.Sha256));
    }

    // RFC 6238 SHA-512 test key: 64 bytes ASCII "1234567890123456789012345678901234567890123456789012345678901234"
    private static readonly byte[] RfcSha512SecretBytes = Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234");

    [Theory]
    [InlineData(59L, "90693936")]
    [InlineData(1111111109L, "25091201")]
    [InlineData(1111111111L, "99943326")]
    [InlineData(1234567890L, "93441116")]
    [InlineData(2000000000L, "38618901")]
    [InlineData(20000000000L, "47863826")]
    public void GenerateTotp_Sha512_MatchesRfc6238TestVectors(long unixSeconds, string expected8Digits)
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        string otp = _otpService.GenerateOtpToken(RfcSha512SecretBytes, timestamp, stepSeconds: 30, digits: 8, OtpHashAlgorithm.Sha512);
        Assert.Equal(expected8Digits, otp);

        Assert.True(_otpService.CheckOtpTokenValidity(RfcSha512SecretBytes, expected8Digits, timestamp, toleranceSteps: 0, stepSeconds: 30, digits: 8, OtpHashAlgorithm.Sha512));
    }
}
