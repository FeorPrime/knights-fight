using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Entities;

namespace KnightFight.Game.Engine;

/// <summary>
/// Interface representing an active or completed KnightFight game instance.
/// </summary>
public interface IGame
{
    Guid Id { get; }

    /// <summary>
    /// The moment the match was created, in UTC.
    /// </summary>
    DateTime StartedAt { get; }

    int TurnNumber { get; }
    GameStatus Status { get; }
    GameWinner Winner { get; }
    BoardBounds Bounds { get; }

    Fighter Fighter1 { get; }
    Fighter Fighter2 { get; }
    IReadOnlyList<BonusEntity> Bonuses { get; }

    bool HasChallenger(string challengerId);
    Fighter? GetFighter(string challengerId);
    bool EnqueueAction(string challengerId, GameAction action);
    IReadOnlyList<GameAction> GetBufferedActions(string challengerId);
    void ClearBufferedActions(string challengerId);

    TurnResult CalculateTurn();
    GameSnapshot CreateSnapshot();
}
