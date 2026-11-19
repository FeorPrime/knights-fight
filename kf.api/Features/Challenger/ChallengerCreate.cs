using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Models;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Api.Models.Mappers;
using KnightFight.Api.Models.Requests;
using KnightFight.Api.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Challenger;

public class ChallengerCreate(
    IDbContextFactory<KfContext> factory,
    INewcomerNotificationService notificationService,
    IDefaultAvatarProvider defaultAvatars)
    : EncryptedPostEndpoint<CreateChallengerRequest>(ApiMap.Challenger.CREATE)
{
    private const string CANT_SAVE_CHALLENGER = "Challenger was not saved! Already Exists!";

    private async Task<ExceptResult<CreateChallengerResponse>> CreateNewChallenger(CreateChallengerRequest request, KfContext ctx)
    {
        var challengerAlreadyExists = ctx.Challengers.Any(e => e.ChallengerName == request.ChallengerName);
        if (challengerAlreadyExists)
            return ExceptResult<CreateChallengerResponse>.Failure(CANT_SAVE_CHALLENGER);
        
        var en = ctx.Challengers.Add(
            request.ToEntity(
                await CryptoService.GetHash(request.Secret),
                defaultAvatars.RandomIndex()
            )
        );

        var result = await ctx.SaveChangesAsync();

        if (result > 0)
        {
            await notificationService.PublishAsync(new ChallengerNewcomerEvent(en.Entity.Id, en.Entity.ChallengerName, en.Entity.CreatedAt, AvatarUrl.For(en.Entity.Id)));
            return ExceptResult<CreateChallengerResponse>.Success(new CreateChallengerResponse(en.Entity.Id, en.Entity.ChallengerName));
        }

        return ExceptResult<CreateChallengerResponse>.Failure(CANT_SAVE_CHALLENGER);
    }

    [Tags("Challenger")]
    [EndpointSummary("create new challenger")]
    [EndpointDescription("Encrypted endpoint to create new challenger")]
    public override async Task<IResult> Handler([FromBody] EncryptedRequest data)
    {
        return await base.Handler(data);
    }
    
    protected override async Task<IResult> Handle(CreateChallengerRequest request, string? openPayload)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var result = await CreateNewChallenger(request, ctx);

        return result.IsSuccess ? Results.Created(UriPattern, result) 
            : Results.Conflict(request.ChallengerName);
    }
}
