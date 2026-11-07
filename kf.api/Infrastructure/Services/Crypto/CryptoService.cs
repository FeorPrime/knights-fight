using System.Security.Cryptography;
using System.Text;
using KnightFight.Api.Models.Responses;
using Org.BouncyCastle.Crypto.Digests;

namespace KnightFight.Api.Infrastructure.Services.Crypto;

/// <summary>
/// Provides cryptographic utilities for generating, encrypting, and decrypting data,
/// as well as managing cryptographic keys.
/// </summary>
public static class CryptoService //: ICryptoService
{
    private const int AES_KEY_SIZE_BYTES = 32;   // AES-256
    private const int NONCE_SIZE_BYTES  = 12;   // GCM standard nonce
    private const int TAG_SIZE_BYTES    = 16;   // GCM 128-bit tag
    
    private static readonly RSA _rsa = RSA.Create(2048); //!WARNING!: in real app should be populated from secrets store
    internal static readonly string STATIC_KEY = GenerateJwtSecret(); //!WARNING!: in real app should be populated from secrets store
    internal static readonly byte[] STATIC_KEY_BYTES = Encoding.UTF8.GetBytes(STATIC_KEY); //!WARNING!: in real app should be populated from secrets store
    
    public static async Task<string> GetPublicKeyPem() => _rsa.ExportSubjectPublicKeyInfoPem();
    
    /// <summary>
    /// Generates a cryptographically "secure" Base64 encoded string.
    /// </summary>
    /// <param name="sizeInBytes">The number of random bytes to generate. 
    /// 32 bytes (256 bits) is minimum for HS256. 64 bytes (512 bits) is recommended.</param>
    private static string GenerateJwtSecret(int sizeInBytes = 64)
    {
        // RandomNumberGenerator is thread-safe and cryptographically secure
        byte[] randomBytes = RandomNumberGenerator.GetBytes(sizeInBytes);
        
        // Convert to Base64 so it can be easily saved in config files
        var result = Convert.ToBase64String(randomBytes);
        
    #if DEBUG
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("DEBUG! STATIC_KEY: {0}", result);
        Console.ResetColor();
    #endif
        
        //Log.Debug("DEBUG! STATIC_KEY: {Key}", result);
        
        return result;
    }

    public static async Task<AuthTokenResponse> Encrypt(string clientPem, string data)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(clientPem);
        
        // 2. Generate ephemeral AES-256 key and GCM nonce
        Span<byte> aesKey = stackalloc byte[AES_KEY_SIZE_BYTES];
        Span<byte> nonce  = stackalloc byte[NONCE_SIZE_BYTES];
        RandomNumberGenerator.Fill(aesKey);
        RandomNumberGenerator.Fill(nonce);
        ReadOnlySpan<byte> plaintext = Encoding.UTF8.GetBytes(data);
        
        // 3. Encrypt plaintext with AES-GCM
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag        = new byte[TAG_SIZE_BYTES];
        
        using (var aesGcm = new AesGcm(aesKey, TAG_SIZE_BYTES))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        
        // 4. Wrap the AES key with the client's RSA public key (OAEP-SHA256)
        byte[] encryptedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        
        // 5. Zero out the AES key material after use
        CryptographicOperations.ZeroMemory(aesKey);

        return new AuthTokenResponse(
            EncryptedKey: Convert.ToBase64String(encryptedKey),
            Nonce:        Convert.ToBase64String(nonce),
            Ciphertext:   Convert.ToBase64String(ciphertext),
            Tag:          Convert.ToBase64String(tag));
    }
    
    public static async Task<string> Decrypt(string base64EncryptedData)
    {
        var encryptedBytes = Convert.FromBase64String(base64EncryptedData);
        var decryptedBytes = _rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
    
    public static async Task<string> GetHash(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);

        // BouncyCastle требует передать размер хеша в битах
        var sha3 = new Sha3Digest(512);
        sha3.BlockUpdate(inputBytes, 0, inputBytes.Length);

        byte[] hashBytes = new byte[sha3.GetDigestSize()];
        sha3.DoFinal(hashBytes, 0);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}