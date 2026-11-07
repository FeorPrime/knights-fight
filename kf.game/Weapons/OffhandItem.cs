namespace KnightFight.Game.Weapons;

/// <summary>
/// Offhand items for fighters.
/// </summary>
public enum OffhandItem
{
    /// <summary>
    /// Ignores damage coming from the front if used during the turn.
    /// </summary>
    Shield = 0,

    /// <summary>
    /// Pushes whatever is in front 2 squares away (no damage dealt).
    /// </summary>
    Hammer = 1,

    /// <summary>
    /// Catches whatever is up to 3 squares in front and moves it 1 square towards you.
    /// </summary>
    GrapplingHook = 2
}
