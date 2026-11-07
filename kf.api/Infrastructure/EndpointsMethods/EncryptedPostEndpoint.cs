using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Api.Models.Mappers;
using KnightFight.Api.Models.Requests;
using Microsoft.AspNetCore.Mvc;

namespace KnightFight.Api.Infrastructure.EndpointsMethods;

/// <summary>
/// Represents an abstract base class for a POST endpoint whose body carries a payload
/// encrypted with the server's public key.
/// This class provides the decrypt - deserialize - dispatch chain, so that a concrete
/// endpoint only has to implement its own domain logic.
/// </summary>
/// <typeparam name="TRequest">The type the decrypted payload is deserialized into.</typeparam>
/// <remarks>
/// The encrypted envelope is <see cref="EncryptedRequest"/>: <c>Payload</c> holds the encrypted
/// body and <c>OpenPayload</c> the unencrypted part (for example the client's public key that the
/// response has to be encrypted with).
/// The concrete endpoint supplies the deserializer, usually one of the source generated
/// deserializers in <c>Models/Mappers</c>.
/// </remarks>
public abstract class EncryptedPostEndpoint<TRequest>(string uriPattern)
    : PostEndpoint<EncryptedRequest>(uriPattern)
    where TRequest : class
{
    private const string JSON_PROCESS_ERROR = "Skipping processing due to invalid JSON payload.";
    private const string EMPTY_REQUEST = "Empty request can't be handled!";
    
    /// <summary>
    /// Deserializes the decrypted payload.
    /// </summary>
    /// <param name="json">The decrypted JSON string.</param>
    /// <param name="result">The deserialized request, or null when the payload is invalid.</param>
    /// <returns>True when the payload could be deserialized; otherwise, false.</returns>
    private static bool TryDeserialize(string json, out TRequest? result) => RequestDeserializer.TryDeserializeMyClass<TRequest>(json, out result);

    /// <summary>
    /// Handles the decrypted request.
    /// </summary>
    /// <param name="request">The deserialized request.</param>
    /// <param name="openPayload">The unencrypted part of the envelope.</param>
    protected abstract Task<IResult> Handle(TRequest request, string? openPayload);

    // [FromBody] is declared here on purpose: the framework binds the most derived
    // override, and binding attributes are not inherited from the base declaration.
    public override async Task<IResult> Handler([FromBody] EncryptedRequest data)
    {
        if (string.IsNullOrEmpty(data.Payload)) return Results.BadRequest(EMPTY_REQUEST);

        var decrypted = await CryptoService.Decrypt(data.Payload!);

        if (!TryDeserialize(decrypted, out var request))
            throw new CantProcessRequestException(JSON_PROCESS_ERROR);

        return await Handle(request!, data.OpenPayload);
    }

    /// <summary>
    /// Raised when the decrypted payload cannot be deserialized into <typeparamref name="TRequest"/>.
    /// </summary>
    public class CantProcessRequestException(string message) : Exception(message);
}
