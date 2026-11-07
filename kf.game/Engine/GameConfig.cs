using KnightFight.Game.Common;

namespace KnightFight.Game.Engine;

/// <summary>
/// Configuration for a KnightFight match.
/// Default values strictly match the specification.
/// </summary>
public sealed record GameConfig
{
    public Position Fighter1StartPosition { get; init; } = new(1, 1);
    public Direction Fighter1StartDirection { get; init; } = Direction.East;

    public Position Fighter2StartPosition { get; init; } = new(18, 18);
    public Direction Fighter2StartDirection { get; init; } = Direction.West;

    public Position Bonus1Position { get; init; } = new(5, 14);
    public Position Bonus2Position { get; init; } = new(14, 5);

    public int InitialFighterHp { get; init; } = 3;
    public int InitialBonusHp { get; init; } = 1;
    public int TurnsPerShrink { get; init; } = 5;
    public int ShrinkAmount { get; init; } = 1;
    public int OutOfBoundsDamagePerTurn { get; init; } = 1;
    public int MaxBufferedMoves { get; init; } = 3;

    public BoardBounds InitialBounds { get; init; } = BoardBounds.Default20x20;

    public static GameConfig Default => new();
}

public enum GameStatus
{
    NotStarted = 0,
    InProgress = 1,
    Finished = 2
}

public enum GameWinner
{
    None = 0,
    Fighter1 = 1,
    Fighter2 = 2,
    Draw = 3
}
