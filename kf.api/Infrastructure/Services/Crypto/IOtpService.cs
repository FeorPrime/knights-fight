namespace KnightFight.Api.Infrastructure.Services.Crypto;

public interface IOtpService
{
    /// <summary>
    /// Generates a cryptographically secure random OTP seed (Base32 encoded).
    /// </summary>
    /// <param name="byteCount">The number of random bytes for the seed (default 20 bytes = 160 bits).</param>
    /// <returns>A Base32 encoded seed string.</returns>
    string GenerateOtpSeed(int byteCount = 20);

    /// <summary>
    /// Generates cryptographically secure random bytes for an OTP seed.
    /// </summary>
    byte[] GenerateSeedBytes(int byteCount = 20);

    /// <summary>
    /// Generates a cryptographically secure Base64 encoded OTP seed.
    /// </summary>
    string GenerateBase64Seed(int byteCount = 20);

    /// <summary>
    /// Generates a time-based one-time password (TOTP - RFC 6238) token for the given seed.
    /// </summary>
    /// <param name="seed">The OTP seed (Base32, Base64, Hex, or raw text).</param>
    /// <param name="time">The timestamp (defaults to UtcNow).</param>
    /// <param name="stepSeconds">The time step in seconds (default 30 seconds).</param>
    /// <param name="digits">Number of digits in the OTP token (default 6).</param>
    /// <param name="algorithm">The hash algorithm (default SHA1).</param>
    /// <returns>The generated zero-padded OTP token string.</returns>
    string GenerateOtpToken(string seed, DateTimeOffset? time = null, int stepSeconds = 30, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Generates a time-based one-time password (TOTP - RFC 6238) token for the given seed bytes.
    /// </summary>
    string GenerateOtpToken(byte[] seedBytes, DateTimeOffset? time = null, int stepSeconds = 30, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Generates an HMAC-based one-time password (HOTP - RFC 4226) token for the given counter.
    /// </summary>
    string GenerateHotp(string seed, long counter, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Generates an HMAC-based one-time password (HOTP - RFC 4226) token for the given counter.
    /// </summary>
    string GenerateHotp(byte[] seedBytes, long counter, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Checks validity of an OTP token against a registered seed.
    /// </summary>
    /// <param name="seed">The registered OTP seed.</param>
    /// <param name="token">The token to check.</param>
    /// <param name="time">The timestamp to validate against (defaults to UtcNow).</param>
    /// <param name="toleranceSteps">The number of time steps before and after the current step to accept (default 1).</param>
    /// <param name="stepSeconds">The time step in seconds (default 30).</param>
    /// <param name="digits">Number of digits in the OTP token (default 6).</param>
    /// <param name="algorithm">The hash algorithm (default SHA1).</param>
    /// <returns>True if the token is valid for the registered seed within the tolerance window, false otherwise.</returns>
    bool CheckOtpTokenValidity(string seed, string token, DateTimeOffset? time = null, int toleranceSteps = 1, int stepSeconds = 30, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Checks validity of an OTP token against registered seed bytes.
    /// </summary>
    bool CheckOtpTokenValidity(byte[] seedBytes, string token, DateTimeOffset? time = null, int toleranceSteps = 1, int stepSeconds = 30, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Checks validity of an HOTP token against a registered seed and counter.
    /// </summary>
    bool ValidateHotp(string seed, string token, long counter, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Checks validity of an HOTP token against registered seed bytes and counter.
    /// </summary>
    bool ValidateHotp(byte[] seedBytes, string token, long counter, int digits = 6, OtpHashAlgorithm algorithm = OtpHashAlgorithm.Sha1);

    /// <summary>
    /// Generates a standard Key Provisioning URI (otpauth://totp/...) for authenticator apps.
    /// </summary>
    string GenerateProvisioningUri(string issuer, string accountName, string seed);
}
