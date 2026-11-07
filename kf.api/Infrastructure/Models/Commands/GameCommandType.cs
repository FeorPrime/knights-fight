namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Strongly typed command categories for game input
/// </summary>
public enum CommandCategory : byte
{
    Movement = 0,
    Attack = 1,
    Turn = 2
}

/// <summary>
/// Which limb performs a strike (readme spec: hit_main / hit_off)
/// </summary>
public enum StrikeSide : byte
{
    Main = 0,
    Off = 1
}

/// <summary>
/// Turn directions (game-dev standard)
/// </summary>
public enum TurnDirection : byte
{
    Left = 0,
    Right = 1
}
