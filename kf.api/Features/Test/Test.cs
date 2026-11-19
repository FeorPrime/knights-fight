using KnightFight.Api.Infrastructure.EndpointsMethods;
using Microsoft.AspNetCore.Authorization;

namespace KnightFight.Api.Features.Test;

public class Test(ILogger<Test> logger) : GetEndpoint("/api/test/auth")
{
    [Authorize]
    [Tags("Test")]
    [EndpointSummary("Test for jwt token")]
    [EndpointDescription("Method for auth testing. You should provide Authenticate header with valid bearer token.")]
    public override async Task<IResult> Handler()
    {
        logger.LogInformation("REQUEST ACCEPTED");
        
        return Results.Ok("request accepted");
    }
}