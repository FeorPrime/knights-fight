namespace KnightFight.Game.Common;

/// <summary>
/// Represents a 2D integer coordinate on the game grid (0-based).
/// </summary>
public readonly record struct Position(int X, int Y)
{
    public Position Offset(int dx, int dy) => new(X + dx, Y + dy);

    public Position Move(Direction direction, int steps = 1)
    {
        var (dx, dy) = direction.ToDelta();
        return new Position(X + dx * steps, Y + dy * steps);
    }

    public int ManhattanDistance(Position other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public int ChebyshevDistance(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public override string ToString() => $"({X}, {Y})";
}
