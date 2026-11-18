using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.EndpointHandlers;
using Microsoft.AspNetCore.Mvc;

namespace KnightFight.Api.Infrastructure.EndpointsMethods;

/// <summary>
/// Represents an abstract POST endpoint that defines a specific URI pattern
/// and a request handler for processing incoming POST requests.
/// </summary>
/// <typeparam name="TRequest">The type of the request payload expected by the handler.</typeparam>
public abstract class PostEndpoint<TRequest>(string uriPattern) : ApiMethod(uriPattern), IRequestHandler<TRequest>
{
    public override void MapEndpoint(WebApplication app)
    {
        if (string.IsNullOrEmpty(UriPattern)) throw new ArgumentNullException(nameof(UriPattern));
        app.MapPost(UriPattern, Handler)
            .WithGroupName("PostEndpoints");;
    }

    public abstract Task<IResult> Handler([FromBody]TRequest request);
    
}