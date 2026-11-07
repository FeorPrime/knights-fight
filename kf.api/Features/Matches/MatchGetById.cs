using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Models.Responses;
using KnightFight.Game.Management;
using Microsoft.AspNetCore.Http;

namespace KnightFight.Api.Features.Matches;

public class MatchGetById(IGameManager gameManager) : ApiMethod(ApiMap.Match.GET_BY_ID)
{
    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, (string id) => GetMatch(id));
    }

    private IResult GetMatch(string id)
    {
        if (!Guid.TryParse(id, out var gameId))
        {
            return Results.NotFound(new { message = $"Match {id} not found." });
        }

        var game = gameManager.GetGame(gameId);
        if (game is null)
        {
            return Results.NotFound(new { message = $"Match {id} not found." });
        }

        return Results.Ok(new MatchDto(
            game.Id.ToString(),
            game.Fighter1.Name,
            game.Fighter2.Name,
            game.Status.ToString(),
            game.StartedAt));
    }
}
