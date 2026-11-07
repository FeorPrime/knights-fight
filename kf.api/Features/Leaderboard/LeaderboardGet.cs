using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Models.Mappers;
using KnightFight.Api.Models.Requests;
using KnightFight.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Leaderboard;

public class LeaderboardGet(IDbContextFactory<KfContext> factory)
    : GetFilteredEndpoint<GetLeaderboardRequest>(ApiMap.Leaderboard.GET_TOP)
{
    private const int DEFAULT_LIMIT = 20;
    private const int MIN_LIMIT = 1;
    private const int MAX_LIMIT = 100;

    public override async Task<IResult> Handler([AsParameters] GetLeaderboardRequest request)
    {
        var limit = Math.Clamp(request.Limit ?? DEFAULT_LIMIT, MIN_LIMIT, MAX_LIMIT);

        await using var ctx = await factory.CreateDbContextAsync();

        var rankedEntities = await ctx.Challengers
            .AsNoTracking()
            .OrderByDescending(c => c.Wins * 3 + c.Draws)
            .ThenByDescending(c => c.Wins)
            .ThenBy(c => c.Id)
            .Take(limit)
            .Select(c => new
            {
                c.Id,
                c.ChallengerName,
                c.Wins,
                c.Loses,
                Score = c.Wins * 3 + c.Draws
            })
            .ToListAsync();

        var result = rankedEntities
            .Select((c, index) => new RankedChallengerResponse(
                c.Id,
                c.ChallengerName,
                index + 1,
                c.Wins,
                c.Loses,
                c.Score,
                AvatarUrl.For(c.Id)
            ))
            .ToList();

        return Results.Ok(result);
    }
}
