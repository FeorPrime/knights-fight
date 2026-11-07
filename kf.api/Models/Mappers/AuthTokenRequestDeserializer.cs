using System.Text.Json;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Models.Requests;

namespace KnightFight.Api.Models.Mappers;

public class AuthTokenRequestDeserializer
{
    /// <summary>
    /// Safely attempts to deserialize a JSON string into MyClass.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="result">The resulting MyClass instance, or null if it fails.</param>
    /// <param name="logger">Optional logger for tracking failures.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public static bool TryDeserializeMyClass(
        string? json, 
        out AuthTokenRequest? result, 
        ILogger? logger = null)
    {
        result = null;

        // 1. Guard clause for empty/null inputs
        if (string.IsNullOrWhiteSpace(json))
        {
            logger?.LogWarning("Deserialization failed: Input JSON is null or whitespace.");
            return false;
        }

        try
        {
            // 2. Use the Source Generator context (AppJsonContext.Default)
            // This is vastly faster and safer than standard reflection-based deserialization.
            result = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthTokenRequest);

            if (result is null)
            {
                logger?.LogWarning("Deserialization resulted in a null object despite valid JSON structure.");
                return false;
            }

            // 3. Optional: Add domain-specific validation here if needed
            // if (result.Id <= 0) return false;

            return true;
        }
        catch (JsonException ex)
        {
            // SECURITY NOTE: Do NOT log the raw `json` variable here. 
            // It may contain PII, secrets, or be used for Log Injection attacks.
            logger?.LogError(ex, "Failed to deserialize JSON to MyClass. Reason: {ErrorMessage}", ex.Message);
            return false;
        }
        catch (NotSupportedException ex)
        {
            logger?.LogError(ex, "Unsupported type or operation encountered during JSON deserialization.");
            return false;
        }
    }
}