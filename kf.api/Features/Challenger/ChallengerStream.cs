using System.Text.Json.Serialization.Metadata;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Responses;

namespace KnightFight.Api.Features.Challenger;

public class ChallengerStream(INewcomerNotificationService notificationService)
    : StreamEndpoint<ChallengerNewcomerEvent>(ApiMap.Challenger.STREAM)
{
    protected override JsonTypeInfo<ChallengerNewcomerEvent> EventTypeInfo =>
        AppJsonContext.Default.ChallengerNewcomerEvent;

    [Tags("Challenger")]
    [EndpointSummary("Get newcomers stream data")]
    [EndpointDescription("Show all players, who handle registration in last few minutes")]
    public override async Task Stream(IHttpContextAccessor contextAccessor, CancellationToken cancellationToken)
    {
        await base.Stream(contextAccessor, cancellationToken);
    }

    
    protected override IAsyncEnumerable<ChallengerNewcomerEvent> ReadEvents(CancellationToken cancellationToken)
        => notificationService.SubscribeAsync(cancellationToken);
}
