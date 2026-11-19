using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Services.Crypto;

namespace KnightFight.Api.Features.Auth;

public class PublicKeyGet() : GetEndpoint(ApiMap.Auth.GET_PUBLIC_KEY)
{
    [Tags("Auth")]
    [EndpointSummary("provides public key for encrypted endpoints")]
    [EndpointDescription("provides public key for encrypted endpoints")]
    public override Task<IResult> Handler()
    {
        try
        {
            return Task.FromResult(Results.Ok(CryptoService.GetPublicKeyPem()));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResult>(exception);
        }
    }
}