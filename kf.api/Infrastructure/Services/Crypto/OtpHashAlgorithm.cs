namespace KnightFight.Api.Infrastructure.Services.Crypto;

/// <summary>
/// Specifies the hash algorithm to be used in the generation of one-time passwords (OTP).
/// </summary>
public enum OtpHashAlgorithm
{
    Sha1,
    Sha256,
    Sha512
}
