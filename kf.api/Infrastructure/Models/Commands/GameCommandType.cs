namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Strongly typed command categories for game input
/// </summary>
public enum CommandCategory : byte
{
    Movement = 0,
    HandAction = 1,
    Turn = 2
}

/// <summary>
/// Movement directions (game-dev standard)
/// </summary>
public enum MovementDirection : byte
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3
}

/// <summary>
/// Hand selection for actions
/// </summary>
public enum HandSide : byte
{
    Left = 0,
    Right = 1
}

/// <summary>
/// Turn directions (game-dev standard)
/// </summary>
public enum TurnDirection : byte
{
    Left = 0,
    Right = 1
}
