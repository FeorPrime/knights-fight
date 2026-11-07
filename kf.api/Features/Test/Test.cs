using KnightFight.Api.Infrastructure.EndpointsMethods;
using Microsoft.AspNetCore.Authorization;

namespace KnightFight.Api.Features.Test;

public class Test(ILogger<Test> logger) : GetEndpoint("/api/test")
{
    [Authorize]
    public override async Task<IResult> Handler()
    {
        logger.LogInformation("REQUEST ACCEPTED");
        
        return Results.Ok("request accepted");
    }
}