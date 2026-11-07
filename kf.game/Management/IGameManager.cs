using KnightFight.Game.Actions;
using KnightFight.Game.Engine;

namespace KnightFight.Game.Management;

/// <summary>
/// Service managing game instances, allowing game lookup by ID or challenger ID.
/// </summary>
public interface IGameManager
{
    /// <summary>
    /// Creates and registers a new game between two challengers.
    /// </summary>
    IGame CreateGame(
        string challenger1Id,
        string challenger2Id,
        string? challenger1Name = null,
        string? challenger2Name = null,
        GameConfig? config = null,
        IRandomProvider? randomProvider = null);

    /// <summary>
    /// Finds a game by its unique ID.
    /// </summary>
    IGame? GetGame(Guid gameId);

    /// <summary>
    /// Finds the active game for a given challenger (or the most recent if multiple exist).
    /// </summary>
    IGame? GetGameByChallenger(string challengerId);

    /// <summary>
    /// Returns all games associated with a given challenger.
    /// </summary>
    IReadOnlyList<IGame> GetGamesByChallenger(string challengerId);

    /// <summary>
    /// Returns all active (in-progress) games.
    /// </summary>
    IReadOnlyList<IGame> GetActiveGames();

    /// <summary>
    /// Returns all registered games.
    /// </summary>
    IReadOnlyList<IGame> GetAllGames();

    /// <summary>
    /// Enqueues an action for a challenger in a specific game.
    /// </summary>
    bool EnqueueAction(Guid gameId, string challengerId, GameAction action);

    /// <summary>
    /// Calculates a single turn for the specified game.
    /// </summary>
    TurnResult? TickGame(Guid gameId);

    /// <summary>
    /// Calculates a turn for all active games simultaneously.
    /// </summary>
    IReadOnlyDictionary<Guid, TurnResult> TickAllActiveGames();

    /// <summary>
    /// Removes a game from the manager.
    /// </summary>
    bool RemoveGame(Guid gameId);
}
