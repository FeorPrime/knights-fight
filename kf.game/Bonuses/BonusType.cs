namespace KnightFight.Game.Bonuses;

/// <summary>
/// All bonus types specified in the game design.
/// </summary>
public enum BonusType
{
    /// <summary>
    /// Additional hit point (+1 HP).
    /// </summary>
    Heart = 0,

    /// <summary>
    /// Move action advances 2 squares instead of 1.
    /// </summary>
    Horse = 1,

    /// <summary>
    /// Move action moves backwards instead of forwards.
    /// </summary>
    Reverse = 2,

    /// <summary>
    /// Whoever stands adjacent at the end of the turn is pushed 1 square away.
    /// </summary>
    Ring = 3,

    /// <summary>
    /// Immediately swap positions with the opponent upon collection.
    /// </summary>
    Tp = 4,

    /// <summary>
    /// Immediately deal 1 damage to the opponent upon collection.
    /// </summary>
    Bolt = 5,

    /// <summary>
    /// Immediately deal 1 damage to both fighters upon collection.
    /// </summary>
    Storm = 6
}
