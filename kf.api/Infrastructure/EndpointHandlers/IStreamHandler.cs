namespace KnightFight.Api.Infrastructure.EndpointHandlers;

public interface IStreamHandler
{
    Task Stream(IHttpContextAccessor context, CancellationToken cancellationToken);
}
