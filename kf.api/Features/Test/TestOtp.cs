using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;

namespace KnightFight.Api.Features.Test;

public class TestOtp(IOtpService otpService, IOtpSeedsProvider seedsProvider) : GetFilteredEndpoint<TestOtpRequest>("/api/test-otp")
{
    
    public override async Task<IResult> Handler([AsParameters] TestOtpRequest request)
    {
        var seed = await seedsProvider.Get(request.ChallengerName);
        if (seed.IsError) { return Results.Forbid(); }

        var validCode = otpService.CheckOtpTokenValidity(seed.ResultValue!, request.OtpCode);
        return validCode ? Results.Ok("OTP Code Accepted!") : Results.Forbid();
    }
}

public record TestOtpRequest(string ChallengerName, string OtpCode);