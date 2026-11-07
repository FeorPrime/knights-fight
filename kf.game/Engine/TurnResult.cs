using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Engine;

/// <summary>
/// Immutable snapshot of a fighter's state.
/// </summary>
public sealed record FighterSnapshot(
    string Id,
    string Name,
    Position Position,
    Direction Direction,
    int HitPoints,
    int MaxHitPoints,
    MainWeapon MainWeapon,
    OffhandItem OffhandItem,
    IReadOnlyCollection<BonusType> ActiveBonuses,
    bool IsShieldActive,
    bool IsAlive,
    int BufferedMovesCount
);

/// <summary>
/// Immutable snapshot of a bonus entity state.
/// </summary>
public sealed record BonusSnapshot(
    Guid Id,
    BonusType Type,
    Position Position,
    int HitPoints,
    bool IsCollected,
    bool IsDestroyed
);

/// <summary>
/// Immutable snapshot of the entire game state at a given point in time.
/// </summary>
public sealed record GameSnapshot(
    Guid GameId,
    int TurnNumber,
    GameStatus Status,
    GameWinner Winner,
    BoardBounds Bounds,
    FighterSnapshot Fighter1,
    FighterSnapshot Fighter2,
    IReadOnlyList<BonusSnapshot> Bonuses
);

/// <summary>
/// Event describing an action or occurrence during turn calculation.
/// </summary>
public sealed record GameEvent(
    string EventType,
    string Description,
    string? SubjectId = null,
    Position? Position = null,
    int? Value = null
);

/// <summary>
/// Full summary of a resolved turn.
/// </summary>
public sealed record TurnResult(
    int TurnNumber,
    GameAction Fighter1Action,
    GameAction Fighter2Action,
    BoardBounds BoundsBefore,
    BoardBounds BoundsAfter,
    bool DidBoardShrink,
    IReadOnlyList<GameEvent> Events,
    GameSnapshot StateAfterTurn
);
