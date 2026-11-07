namespace KnightFight.Api.Infrastructure.EndpointHandlers;

public interface INoParamsHandler
{
    Task<IResult> Handler();
}