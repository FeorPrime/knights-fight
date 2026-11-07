using System.Collections.Concurrent;
using KnightFight.Game.Actions;
using KnightFight.Game.Engine;

namespace KnightFight.Game.Management;

/// <summary>
/// Thread-safe manager that tracks all games and allows lookup by Game ID or Challenger ID.
/// </summary>
public sealed class GameManager : IGameManager
{
    private readonly ConcurrentDictionary<Guid, IGame> _games = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _challengerIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;

    public GameManager(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IGame CreateGame(
        string challenger1Id,
        string challenger2Id,
        string? challenger1Name = null,
        string? challenger2Name = null,
        GameConfig? config = null,
        IRandomProvider? randomProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(challenger1Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(challenger2Id);

        var game = new Engine.Game(
            challenger1Id,
            challenger2Id,
            challenger1Name,
            challenger2Name,
            config,
            randomProvider,
            null,          // gameId - let the game generate one
            _timeProvider
        );

        _games[game.Id] = game;

        IndexChallenger(challenger1Id, game.Id);
        IndexChallenger(challenger2Id, game.Id);

        return game;
    }

    public IGame? GetGame(Guid gameId) =>
        _games.TryGetValue(gameId, out var game) ? game : null;

    public IGame? GetGameByChallenger(string challengerId)
    {
        if (string.IsNullOrWhiteSpace(challengerId)) return null;

        var games = GetGamesByChallenger(challengerId);
        // Prioritize in-progress games first, otherwise the most recently added game
        return games.FirstOrDefault(g => g.Status == GameStatus.InProgress) ?? games.LastOrDefault();
    }

    public IReadOnlyList<IGame> GetGamesByChallenger(string challengerId)
    {
        if (string.IsNullOrWhiteSpace(challengerId)) return Array.Empty<IGame>();

        if (_challengerIndex.TryGetValue(challengerId, out var gameIds))
        {
            var result = new List<IGame>();
            foreach (var id in gameIds.Keys)
            {
                if (_games.TryGetValue(id, out var game))
                {
                    result.Add(game);
                }
            }
            return result;
        }

        return Array.Empty<IGame>();
    }

    public IReadOnlyList<IGame> GetActiveGames() =>
        _games.Values.Where(g => g.Status == GameStatus.InProgress).ToArray();

    public IReadOnlyList<IGame> GetAllGames() =>
        _games.Values.ToArray();

    public bool EnqueueAction(Guid gameId, string challengerId, GameAction action)
    {
        if (_games.TryGetValue(gameId, out var game))
        {
            return game.EnqueueAction(challengerId, action);
        }
        return false;
    }

    public TurnResult? TickGame(Guid gameId)
    {
        if (_games.TryGetValue(gameId, out var game))
        {
            return game.CalculateTurn();
        }
        return null;
    }

    public IReadOnlyDictionary<Guid, TurnResult> TickAllActiveGames()
    {
        var results = new Dictionary<Guid, TurnResult>();
        foreach (var game in GetActiveGames())
        {
            var result = game.CalculateTurn();
            results[game.Id] = result;
        }
        return results;
    }

    public bool RemoveGame(Guid gameId)
    {
        if (_games.TryRemove(gameId, out var game))
        {
            UnindexChallenger(game.Fighter1.Id, gameId);
            UnindexChallenger(game.Fighter2.Id, gameId);
            return true;
        }
        return false;
    }

    private void IndexChallenger(string challengerId, Guid gameId)
    {
        var bag = _challengerIndex.GetOrAdd(challengerId, _ => new ConcurrentDictionary<Guid, byte>());
        bag[gameId] = 0;
    }

    private void UnindexChallenger(string challengerId, Guid gameId)
    {
        if (_challengerIndex.TryGetValue(challengerId, out var bag))
        {
            bag.TryRemove(gameId, out _);
        }
    }
}
