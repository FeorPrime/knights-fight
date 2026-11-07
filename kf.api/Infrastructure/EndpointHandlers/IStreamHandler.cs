namespace KnightFight.Api.Infrastructure.EndpointHandlers;

public interface IStreamHandler
{
    Task Stream(HttpContext context, CancellationToken cancellationToken);
}
