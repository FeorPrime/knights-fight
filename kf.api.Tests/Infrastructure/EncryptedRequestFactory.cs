using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KnightFight.Api.Tests.Infrastructure;

/// <summary>
/// Posts an RSA-encrypted body the way kf.client does, so contract tests can
/// drive the encrypted endpoints.
/// </summary>
/// <remarks>
/// The inner payload is deserialized with default JsonSerializerOptions, so
/// its property names must be PascalCase: {"ChallengerName":"...","Secret":"..."}.
/// </remarks>
public static class EncryptedRequestFactory
{
    public static async Task<HttpResponseMessage> PostEncryptedAsync(
        HttpClient client,
        string url,
        string innerJson)
    {
        var keyResponse = await client.GetAsync("/api/Auth/public-key");
        keyResponse.EnsureSuccessStatusCode();

        var keyDocument = await keyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var pem = keyDocument.GetProperty("result").GetString()!;

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var cipher = rsa.Encrypt(Encoding.UTF8.GetBytes(innerJson), RSAEncryptionPadding.OaepSHA256);
        var envelope = JsonSerializer.Serialize(new { Payload = Convert.ToBase64String(cipher) });

        return await client.PostAsync(url, new StringContent(envelope, Encoding.UTF8, "application/json"));
    }
}
