using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Models;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Api.Models.Requests;
using KnightFight.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Auth;

public class AuthGetTokens(IAuthService authService, IOtpService otpService, IOtpSeedsProvider seedsProvider, IDbContextFactory<KfContext> factory, ILogger<AuthGetTokens> logger)
    : EncryptedPostEndpoint<AuthTokenRequest>(ApiMap.Auth.LOGIN)
{
    private const string USER_NOT_ALLOWED = "User not allowed!";

    private async Task<ExceptResult<AuthTokenResponse>> GetAuthToken(AuthTokenRequest request, string? publicKey, KfContext kfc)
    {
        //1. Check user creds
        var challenger = await kfc.Challengers.SingleOrDefaultAsync(ch => ch.ChallengerName == request.ChallengerName);
        if (challenger is null || challenger.Secret != await CryptoService.GetHash(request.Secret) || string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(request.Secret))
        {
            return ExceptResult<AuthTokenResponse>.Failure(USER_NOT_ALLOWED);
        }
        
        //2. Generate token
        var jwtToken = authService.GenerateToken(challenger);
        var otpSeed = otpService.GenerateOtpSeed();
        var token = "JWT:" + jwtToken + "__OTP:" + otpSeed;
        
        var seedIsStored = await seedsProvider.ForceAdd(challenger.ChallengerName, otpSeed);
        
        if (seedIsStored.IsError)
        {
            return ExceptResult<AuthTokenResponse>.Failure(seedIsStored.Problem);
        }

        //3. Encrypt key with users public key
        var result = await CryptoService.Encrypt(publicKey, token);
        
        return ExceptResult<AuthTokenResponse>.Success(result);
    }

    protected override async Task<IResult> Handle(AuthTokenRequest request, string? openPayload)
    {
        await using var kfc = await factory.CreateDbContextAsync();

        var result = await GetAuthToken(request, openPayload, kfc);

        if (result.IsSuccess)
        {
            if(logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Token generated successfully");
            return Results.Ok(result);
        }

        if (logger.IsEnabled(LogLevel.Error)) logger.LogError("Token generation failed :{Problem}",result.Problem);
        
        return Results.Forbid();
    }
}
