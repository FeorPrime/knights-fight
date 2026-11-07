using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.EndpointHandlers;

namespace KnightFight.Api.Infrastructure.EndpointsMethods;

/// <summary>
/// Represents an abstract base class for HTTP GET endpoints in the API.
/// </summary>
/// <remarks>
/// This class extends the <see cref="ApiMethod"/> base class and implements the
/// <see cref="INoParamsHandler"/> interface, providing a mechanism to define and map GET endpoints
/// with no parameters.
/// </remarks>
public abstract class GetEndpoint(string uriPattern) : ApiMethod(uriPattern), INoParamsHandler
{
    public override void MapEndpoint(WebApplication app)
    {
        if (string.IsNullOrEmpty(UriPattern)) throw new ArgumentNullException(nameof(UriPattern));
        app.MapGet(UriPattern, Handler);
    }

    public abstract Task<IResult> Handler();
}