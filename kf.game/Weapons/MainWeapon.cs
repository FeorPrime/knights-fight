using KnightFight.Game.Common;

namespace KnightFight.Game.Weapons;

/// <summary>
/// Main weapon types for fighters.
/// </summary>
public enum MainWeapon
{
    /// <summary>
    /// Swings in front 3 squares (left front, center front, right front).
    /// </summary>
    Sword = 0,

    /// <summary>
    /// Pierces 2 squares in front.
    /// </summary>
    Spear = 1,

    /// <summary>
    /// Skips 2 squares in front and hits 2 squares after (distances 3 and 4 in front).
    /// </summary>
    Bow = 2
}

public static class MainWeaponExtensions
{
    /// <summary>
    /// Gets the squares targeted by the main weapon attack from the given origin and facing direction.
    /// </summary>
    public static IReadOnlyList<Position> GetTargetPositions(this MainWeapon weapon, Position origin, Direction direction)
    {
        var (fDx, fDy) = direction.ToDelta();
        var (lDx, lDy) = direction.Left().ToDelta();
        var (rDx, rDy) = direction.Right().ToDelta();

        return weapon switch
        {
            MainWeapon.Sword =>
            [
                new Position(origin.X + fDx + lDx, origin.Y + fDy + lDy), // front-left
                new Position(origin.X + fDx, origin.Y + fDy),             // front-center
                new Position(origin.X + fDx + rDx, origin.Y + fDy + rDy)  // front-right
            ],
            MainWeapon.Spear =>
            [
                new Position(origin.X + fDx, origin.Y + fDy),             // 1 square in front
                new Position(origin.X + fDx * 2, origin.Y + fDy * 2)      // 2 squares in front
            ],
            MainWeapon.Bow =>
            [
                new Position(origin.X + fDx * 3, origin.Y + fDy * 3),     // 3 squares in front (skip 2)
                new Position(origin.X + fDx * 4, origin.Y + fDy * 4)      // 4 squares in front
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null)
        };
    }
}
