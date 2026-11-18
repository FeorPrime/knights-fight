using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.EndpointHandlers;

namespace KnightFight.Api.Infrastructure.EndpointsMethods;

/// <summary>
/// Represents an abstract base class for creating a filtered GET endpoint in the API.
/// This class provides a generic structure for handling HTTP GET requests with query filtering.
/// </summary>
/// <typeparam name="TFilter">The type of the filter object used to process query parameters for the GET request.</typeparam>
/// <remarks>
/// Overrides must apply <c>[AsParameters]</c> to the filter parameter. The framework binds the override
/// (the handler delegate resolves to the most derived implementation) and binding attributes are not
/// inherited from this declaration. <c>[AsParameters]</c> binds every property of the filter from the
/// query string, while <c>[FromQuery]</c> would require the filter to implement TryParse/IParsable.
/// </remarks>
public abstract class GetFilteredEndpoint<TFilter>(string uriPattern) : ApiMethod(uriPattern), IRequestHandler<TFilter>
{
    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, Handler)
            .WithGroupName("GetEndpoints");;
    }

    public abstract Task<IResult> Handler(TFilter request);
}
