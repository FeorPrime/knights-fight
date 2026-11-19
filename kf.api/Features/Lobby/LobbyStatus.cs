using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Mappers;
using KnightFight.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Lobby;

public class LobbyStatus(IOtpSeedsProvider seedsProvider, IDbContextFactory<KfContext> factory) : GetEndpoint(ApiMap.Lobby.STATUS)
{
    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, Handler);
    }

    [Tags("Lobby")]
    [EndpointSummary("Get Lobby data")]
    [EndpointDescription("Shows all players, registred in lobby, also queued for match and their's count")]
    public override async Task<IResult> Handler()
    {
        var users = await seedsProvider.GetLobbyUsers();
        var names = users.Select(u => u.Name).ToList();

        await using var ctx = await factory.CreateDbContextAsync();
        var avatars = await ctx.Challengers
            .AsNoTracking()
            .Where(c => names.Contains(c.ChallengerName))
            .Select(c => new { c.Id, c.ChallengerName })
            .ToDictionaryAsync(c => c.ChallengerName, c => AvatarUrl.For(c.Id));

        var players = users.Select(u => new LobbyPlayerDto(
            u.Name,
            u.IsReady,
            u.Status,
            u.LastActive,
            u.ReadySince,
            avatars.GetValueOrDefault(u.Name)
        )).ToList();

        var queue = players.Select(p => p.Name).ToList();
        var response = new LobbyStatusResponse(players.Count, queue, players);
        return Results.Ok(response);
    }
}
