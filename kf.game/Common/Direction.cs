namespace KnightFight.Game.Common;

/// <summary>
/// Cardinal direction on the grid.
/// Grid coordinate convention:
/// X increases to the East (right), decreases to the West (left).
/// Y increases to the South (down), decreases to the North (up).
/// </summary>
public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public static class DirectionExtensions
{
    public static (int Dx, int Dy) ToDelta(this Direction direction) => direction switch
    {
        Direction.North => (0, -1),
        Direction.East => (1, 0),
        Direction.South => (0, 1),
        Direction.West => (-1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
    };

    public static Direction TurnLeft(this Direction direction) => (Direction)(((int)direction + 3) % 4);

    public static Direction TurnRight(this Direction direction) => (Direction)(((int)direction + 1) % 4);

    public static Direction Opposite(this Direction direction) => (Direction)(((int)direction + 2) % 4);

    public static Direction Left(this Direction direction) => direction.TurnLeft();

    public static Direction Right(this Direction direction) => direction.TurnRight();
}
