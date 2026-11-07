namespace KnightFight.Api.Infrastructure.EndpointHandlers;

public interface IRequestHandler <TRequest>
{
    Task<IResult> Handler(TRequest request);
}