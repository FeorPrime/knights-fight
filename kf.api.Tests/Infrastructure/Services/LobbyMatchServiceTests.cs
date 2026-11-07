using KnightFight.Api.Infrastructure.Services;
using KnightFight.Game.Management;
using Xunit;

namespace KnightFight.Api.Tests.Infrastructure.Services;

/// <summary>
/// Behaviour tests for <see cref="LobbyMatchService"/> - the piece that turns the ready
/// set into running matches.
/// </summary>
public class LobbyMatchServiceTests
{
    private readonly IOtpSeedsProvider _seeds = new OtpSeedsProvider(TimeSpan.FromMinutes(5));
    private readonly GameManager _games = new();
    private readonly LobbyMatchService _service;

    public LobbyMatchServiceTests()
    {
        _service = new LobbyMatchService(_seeds, new Matchmaker(), _games);
    }

    [Fact]
    public async Task TryStartMatches_OneReadyPlayer_StartsNothing()
    {
        await Ready("Alice");

        var outcome = await _service.TryStartMatchesAsync();

        Assert.Empty(outcome.StartedMatches);
        Assert.Empty(_games.GetAllGames());
    }

    [Fact]
    public async Task TryStartMatches_TwoReadyPlayers_StartsOneMatch()
    {
        await Ready("Alice");
        await Ready("Bob");

        var outcome = await _service.TryStartMatchesAsync();

        var match = Assert.Single(outcome.StartedMatches);
        Assert.Equal("Alice", match.Challenger1);
        Assert.Equal("Bob", match.Challenger2);
        Assert.Equal("InProgress", match.Status);
        Assert.Single(_games.GetAllGames());
    }

    [Fact]
    public async Task TryStartMatches_TwoReadyPlayers_PutsThemInTheSameGame()
    {
        await Ready("Alice");
        await Ready("Bob");

        var outcome = await _service.TryStartMatchesAsync();

        var game = Assert.Single(_games.GetAllGames());
        Assert.True(game.HasChallenger("Alice"));
        Assert.True(game.HasChallenger("Bob"));
        Assert.NotNull(outcome.ForPlayer("Alice"));
        Assert.Equal("Bob", outcome.OpponentFor("Alice"));
        Assert.Equal("Alice", outcome.OpponentFor("Bob"));
    }

    [Fact]
    public async Task TryStartMatches_FourReadyPlayers_StartsTwoMatches()
    {
        await Ready("Alice");
        await Ready("Bob");
        await Ready("Carol");
        await Ready("Dave");

        var outcome = await _service.TryStartMatchesAsync();

        Assert.Equal(2, outcome.StartedMatches.Count);
        Assert.Equal(2, _games.GetAllGames().Count);
    }

    [Fact]
    public async Task TryStartMatches_ThreeReadyPlayers_LeavesTheNewestWaiting()
    {
        await Ready("Alice");
        await Ready("Bob");
        await Ready("Carol");

        var outcome = await _service.TryStartMatchesAsync();

        Assert.Single(outcome.StartedMatches);

        var lobby = await _seeds.GetLobbyUsers();
        var stillReady = Assert.Single(lobby, u => u.IsReady);
        Assert.Equal("Carol", stillReady.Name);
    }

    [Fact]
    public async Task TryStartMatches_AfterAMatch_ThePairIsNoLongerReady()
    {
        await Ready("Alice");
        await Ready("Bob");

        await _service.TryStartMatchesAsync();

        var lobby = await _seeds.GetLobbyUsers();
        Assert.DoesNotContain(lobby, u => u.IsReady);
    }

    [Fact]
    public async Task TryStartMatches_CalledAgain_StartsNothingNew()
    {
        await Ready("Alice");
        await Ready("Bob");

        await _service.TryStartMatchesAsync();
        var second = await _service.TryStartMatchesAsync();

        Assert.Empty(second.StartedMatches);
        Assert.Single(_games.GetAllGames());
    }

    [Fact]
    public async Task TryStartMatches_ConcurrentCalls_StartExactlyOneMatch()
    {
        // Two players pressing "ready" at the same moment must not create the same fight twice.
        await Ready("Alice");
        await Ready("Bob");

        var outcomes = await Task.WhenAll(
            Task.Run(() => _service.TryStartMatchesAsync()),
            Task.Run(() => _service.TryStartMatchesAsync()));

        Assert.Single(_games.GetAllGames());
        Assert.Single(outcomes.SelectMany(o => o.StartedMatches));
    }

    private async Task Ready(string name)
    {
        await _seeds.ForceAdd(name, $"SEED-{name}");
        var result = await _seeds.SetReady(name, true);
        Assert.True(result.IsSuccess);
    }
}
