using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Models.Responses;
using KnightFight.Game.Management;

namespace KnightFight.Api.Features.Matches;

public class MatchesGetAll(IGameManager gameManager) : GetEndpoint(ApiMap.Match.GET_ALL)
{
    
    [Tags("Match")]
    [EndpointSummary("Get Matches data")]
    [EndpointDescription("Provides all matches")]
    public override Task<IResult> Handler()
    {
        var matches = gameManager.GetAllGames()
            .OrderByDescending(g => g.StartedAt)
            .Select(g => new MatchDto(
                g.Id.ToString(),
                g.Fighter1.Name,
                g.Fighter2.Name,
                g.Status.ToString(),
                g.StartedAt))
            .ToList();

        return Task.FromResult(Results.Ok(matches));
    }
}
