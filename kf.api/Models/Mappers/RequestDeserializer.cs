using System.Text.Json;

namespace KnightFight.Api.Models.Mappers;

public static class RequestDeserializer
{
    private const string NULL_OR_WHITESPACE_WARNING = "Deserialization failed: Input JSON is null or whitespace";
    private const string RESULT_IS_NULL_WARNING = "Deserialization resulted in a null object despite valid JSON structure";

    private const string FAILED_TO_DESERIALIZE_ERROR_TEMPLATE = "Failed to deserialize JSON to <TOut> class. Reason: {ErrorMessage}";
    private const string UNSUPPORTED_TYPE_OR_OPERATION_ERROR = "Unsupported type or operation encountered during JSON deserialization";
    /// <summary>
    /// Safely attempts to deserialize a JSON string into TOut class.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="result">The resulting MyClass instance, or null if it fails.</param>
    /// <param name="logger">Optional logger for tracking failures.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public static bool TryDeserializeMyClass<TOut>(
        string? json, 
        out TOut? result, 
        ILogger? logger = null) where TOut : class
    {
        result = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            logger?.LogWarning(NULL_OR_WHITESPACE_WARNING);
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<TOut>(json);//, AppJsonContext.Default.CreateChallengerRequest);

            if (result is not null) return true;
            
            logger?.LogWarning(RESULT_IS_NULL_WARNING);
            return false;
        }
        catch (JsonException ex)
        {
            // SECURITY NOTE: Do NOT log the raw `json` variable here. 
            // It may contain PII, secrets, or be used for Log Injection attacks.
            logger?.LogError(ex, FAILED_TO_DESERIALIZE_ERROR_TEMPLATE, ex.Message);
            return false;
        }
        catch (NotSupportedException ex)
        {
            logger?.LogError(ex, UNSUPPORTED_TYPE_OR_OPERATION_ERROR);
            return false;
        }
    }
}