using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KnightFight.Api.Infrastructure.Services.Crypto;

/// <summary>
/// A service for generating and validating one-time passwords (OTP) using various algorithms and encoding formats.
/// </summary>
public class OtpService : IOtpService
{
    private const int DEFAULT_SEED_BYTE_COUNT = 20; // 160 bits (RFC 4226 / RFC 6238 standard for SHA-1)
    private const int DEFAULT_DIGITS = 6;
    private const int DEFAULT_STEP_SECONDS = 30;
    private const int DEFAULT_TOLERANCE_STEPS = 1;

    /// <summary>
    /// Generates a cryptographically secure random OTP seed encoded as a Base32 string.
    /// </summary>
    /// <param name="byteCount">Number of random bytes (default 20 bytes = 160 bits).</param>
    /// <returns>Base32 encoded seed string.</returns>
    public string GenerateOtpSeed(int byteCount = DEFAULT_SEED_BYTE_COUNT)
    {
        if (byteCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be greater than zero.");

        byte[] randomBytes = RandomNumberGenerator.GetBytes(byteCount);
        return Base32.ToBase32String(randomBytes);
    }
    
    public byte[] GenerateSeedBytes(int byteCount = DEFAULT_SEED_BYTE_COUNT)
    {
        if (byteCount <= 0) throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be greater than zero.");

        return RandomNumberGenerator.GetBytes(byteCount);
    }
    
    public string GenerateBase64Seed(int byteCount = DEFAULT_SEED_BYTE_COUNT)
    {
        if (byteCount <= 0) throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be greater than zero.");

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));
    }

    public string GenerateOtpToken(string seed, DateTimeOffset? time = null, int stepSeconds = DEFAULT_STEP_SECONDS, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        byte[] seedBytes = ResolveSeedBytes(seed);
        return GenerateOtpToken(seedBytes, time, stepSeconds, digits, algorithm);
    }

    public string GenerateOtpToken(byte[] seedBytes, DateTimeOffset? time = null, int stepSeconds = DEFAULT_STEP_SECONDS, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        if (seedBytes == null || seedBytes.Length == 0)
            throw new ArgumentException("Seed bytes cannot be null or empty.", nameof(seedBytes));
        if (stepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Step seconds must be greater than zero.");

        var timestamp = time ?? DateTimeOffset.UtcNow;
        long counter = timestamp.ToUnixTimeSeconds() / stepSeconds;

        return GenerateHotp(seedBytes, counter, digits, algorithm);
    }

    public string GenerateHotp(string seed, long counter, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        byte[] seedBytes = ResolveSeedBytes(seed);
        return GenerateHotp(seedBytes, counter, digits, algorithm);
    }

    public string GenerateHotp(byte[] seedBytes, long counter, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        if (seedBytes == null || seedBytes.Length == 0)
            throw new ArgumentException("Seed bytes cannot be null or empty.", nameof(seedBytes));
        if (digits < 1 || digits > 9)
            throw new ArgumentOutOfRangeException(nameof(digits), "Digits must be between 1 and 9.");
        if (counter < 0)
            throw new ArgumentOutOfRangeException(nameof(counter), "Counter must be non-negative.");

        byte[] counterBytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        byte[] hash = algorithm switch
        {
            OtpHashAlgorithm.Sha1 => HMACSHA1.HashData(seedBytes, counterBytes),
            OtpHashAlgorithm.Sha256 => HMACSHA256.HashData(seedBytes, counterBytes),
            OtpHashAlgorithm.Sha512 => HMACSHA512.HashData(seedBytes, counterBytes),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported OTP hash algorithm.")
        };

        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   | (hash[offset + 3] & 0xFF);

        int modulo = (int)Math.Pow(10, digits);
        int otp = binary % modulo;

        return otp.ToString(new string('0', digits));
    }

    public bool CheckOtpTokenValidity(string seed, string token, DateTimeOffset? time = null, int toleranceSteps = DEFAULT_TOLERANCE_STEPS, int stepSeconds = DEFAULT_STEP_SECONDS, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        if (string.IsNullOrWhiteSpace(seed) || string.IsNullOrWhiteSpace(token))
            return false;

        byte[] seedBytes;
        try
        {
            seedBytes = ResolveSeedBytes(seed);
        }
        catch
        {
            return false;
        }

        return CheckOtpTokenValidity(seedBytes, token, time, toleranceSteps, stepSeconds, digits, algorithm);
    }

    public bool CheckOtpTokenValidity(byte[] seedBytes, string token, DateTimeOffset? time = null, int toleranceSteps = DEFAULT_TOLERANCE_STEPS, int stepSeconds = DEFAULT_STEP_SECONDS, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        if (seedBytes.Length == 0)
            return false;
        if (string.IsNullOrWhiteSpace(token))
            return false;
        if (toleranceSteps < 0 || stepSeconds <= 0 || digits < 1 || digits > 9)
            return false;

        string normalizedToken = NormalizeToken(token);
        if (normalizedToken.Length != digits || !normalizedToken.All(char.IsAsciiDigit))
            return false;

        var timestamp = time ?? DateTimeOffset.UtcNow;
        long currentStep = timestamp.ToUnixTimeSeconds() / stepSeconds;

        byte[] tokenBytes = Encoding.UTF8.GetBytes(normalizedToken);

        for (int i = -toleranceSteps; i <= toleranceSteps; i++)
        {
            long step = currentStep + i;
            if (step < 0) continue;

            string expectedToken = GenerateHotp(seedBytes, step, digits, algorithm);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

            if (CryptographicOperations.FixedTimeEquals(expectedBytes, tokenBytes))
            {
                return true;
            }
        }

        return false;
    }
    
    public bool ValidateHotp(string seed, string token, long counter, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        if (string.IsNullOrWhiteSpace(seed) || string.IsNullOrWhiteSpace(token))
            return false;

        byte[] seedBytes;
        try
        {
            seedBytes = ResolveSeedBytes(seed);
        }
        catch
        {
            return false;
        }

        return ValidateHotp(seedBytes, token, counter, digits, algorithm);
    }

    public bool ValidateHotp(byte[] seedBytes, string token, long counter, int digits = DEFAULT_DIGITS, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1)
    {
        if (seedBytes.Length == 0 || string.IsNullOrWhiteSpace(token))
            return false;
        if (digits < 1 || digits > 9 || counter < 0)
            return false;

        string normalizedToken = NormalizeToken(token);
        if (normalizedToken.Length != digits || !normalizedToken.All(char.IsAsciiDigit))
            return false;

        string expectedToken = GenerateHotp(seedBytes, counter, digits, algorithm);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        byte[] tokenBytes = Encoding.UTF8.GetBytes(normalizedToken);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, tokenBytes);
    }

    public string GenerateProvisioningUri(string issuer, string accountName, string seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);

        string encodedIssuer = Uri.EscapeDataString(issuer);
        string encodedAccount = Uri.EscapeDataString(accountName);
        string encodedSeed = Uri.EscapeDataString(seed.Trim());

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={encodedSeed}&issuer={encodedIssuer}";
    }

    private static string NormalizeToken(string token)
    {
        return token.Trim().Replace(" ", "").Replace("-", "");
    }

    private static byte[] ResolveSeedBytes(string seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);

        if (Base32.TryFromBase32String(seed, out var base32Bytes) && base32Bytes.Length > 0)
        {
            return base32Bytes;
        }

        try
        {
            byte[] base64Bytes = Convert.FromBase64String(seed);
            if (base64Bytes.Length > 0) return base64Bytes;
        }
        catch (FormatException) { }

        try
        {
            if (seed.Length % 2 == 0 && seed.All(Uri.IsHexDigit))
            {
                byte[] hexBytes = Convert.FromHexString(seed);
                if (hexBytes.Length > 0) return hexBytes;
            }
        }
        catch (FormatException) { }

        return Encoding.UTF8.GetBytes(seed);
    }
}
