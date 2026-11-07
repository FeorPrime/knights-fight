namespace KnightFight.Game.Common;

/// <summary>
/// Represents the active boundaries of the battlefield.
/// Initial 20x20 field has coordinates from (0, 0) to (19, 19).
/// </summary>
public readonly record struct BoardBounds(int MinX, int MaxX, int MinY, int MaxY)
{
    public static BoardBounds Default20x20 => new(0, 19, 0, 19);

    public int Width => Math.Max(0, MaxX - MinX + 1);
    public int Height => Math.Max(0, MaxY - MinY + 1);

    public bool Contains(Position position) =>
        position.X >= MinX && position.X <= MaxX &&
        position.Y >= MinY && position.Y <= MaxY;

    /// <summary>
    /// Shrinks the battlefield by 1 row and 1 column from each side (all 4 directions).
    /// </summary>
    public BoardBounds Shrink(int amount = 1) =>
        new(MinX + amount, MaxX - amount, MinY + amount, MaxY - amount);

    public override string ToString() => $"[({MinX},{MinY}) .. ({MaxX},{MaxY})]";
}
