namespace KnightFight.Api.Models.Requests;

public record EncryptedRequest
{
    public string Algorithm { get; init; } = "RSA-OAEP-SHA256";
    public string Version { get; init; } = "1.0";
    public string? Payload { get; init; }  // base64-encoded encrypted data
    public string? OpenPayload { get; init; } // not encrypted data
}