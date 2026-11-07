using KnightFight.Game.Management;

namespace KnightFight.Api.Infrastructure.Services;

/// <inheritdoc cref="ILobbyMatchService" />
public sealed class LobbyMatchService(
    IOtpSeedsProvider seedsProvider,
    IMatchmaker matchmaker,
    IGameManager gameManager) : ILobbyMatchService
{
    // Two players can press "ready" at the same moment. Without this gate both requests
    // would read the same ready set and create the same fight twice.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<MatchStartOutcome> TryStartMatchesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var lobby = await seedsProvider.GetLobbyUsers();

            var ready = lobby
                .Where(u => u.IsReady)
                .Select(u => new MatchCandidate(u.Name, u.Name, u.ReadySince ?? u.LastActive))
                .ToList();

            if (ready.Count < 2)
            {
                return new MatchStartOutcome(Array.Empty<StartedMatch>());
            }

            var pairs = matchmaker.PairUp(ready);
            var started = new List<StartedMatch>(pairs.Count);

            foreach (var pair in pairs)
            {
                // The fighter id is the challenger name - the lobby store, the OTP check and
                // the UDP packet all use the name as well.
                var game = gameManager.CreateGame(
                    pair.First.Id,
                    pair.Second.Id,
                    pair.First.Name,
                    pair.Second.Name);

                started.Add(new StartedMatch(
                    game.Id,
                    pair.First.Name,
                    pair.Second.Name,
                    game.Status.ToString(),
                    game.StartedAt));

                // The pair is fighting now - they must not be picked again.
                await seedsProvider.SetReady(pair.First.Id, false);
                await seedsProvider.SetReady(pair.Second.Id, false);
            }

            return new MatchStartOutcome(started);
        }
        finally
        {
            _gate.Release();
        }
    }
}
