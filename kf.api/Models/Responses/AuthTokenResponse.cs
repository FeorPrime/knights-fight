namespace KnightFight.Api.Models.Responses;

public record AuthTokenResponse(    
    string EncryptedKey,   // Base64: AES-256 key, RSA-OAEP-SHA256 encrypted
    string Nonce,          // Base64: 12-byte GCM nonce
    string Ciphertext,     // Base64: ciphertext
    string Tag);           // Base64: 16-byte GCM authentication tag
    