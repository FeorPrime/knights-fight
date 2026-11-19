using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnightFight.Api.Features.Lobby;

public class LobbyJoin(IOtpSeedsProvider seedsProvider, ILobbyMatchService matchService)
    : PostEndpoint<LobbyJoinRequest>(ApiMap.Lobby.JOIN)
{
    [Authorize]
    [Tags("Lobby")]
    [EndpointSummary("Join to the Lobby")]
    [EndpointDescription("Reg player to lobby")]
    public override async Task<IResult> Handler([FromBody] LobbyJoinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengerName))
        {
            return Results.BadRequest(new LobbyJoinResponse(false, "ChallengerName is required."));
        }

        var result = await seedsProvider.SetReady(request.ChallengerName, request.IsReady);
        if (result.IsError)
        {
            return Results.NotFound(new LobbyJoinResponse(false, $"Challenger '{request.ChallengerName}' is not logged in or session expired."));
        }

        // Two ready challengers are enough to start a match.
        var outcome = await matchService.TryStartMatchesAsync();
        var myMatch = outcome.ForPlayer(request.ChallengerName);

        if (myMatch is not null)
        {
            var opponent = outcome.OpponentFor(request.ChallengerName);
            return Results.Ok(new LobbyJoinResponse(
                true,
                $"Match {myMatch.Id} started: {request.ChallengerName} vs {opponent}.",
                myMatch.Id,
                opponent));
        }

        var status = request.IsReady ? "ready" : "unready";
        var response = new LobbyJoinResponse(true, $"Challenger {request.ChallengerName} status updated to {status}.");
        return Results.Ok(response);
    }
}
