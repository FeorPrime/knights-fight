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

    protected override IAsyncEnumerable<ChallengerNewcomerEvent> ReadEvents(CancellationToken cancellationToken)
        => notificationService.SubscribeAsync(cancellationToken);
}
