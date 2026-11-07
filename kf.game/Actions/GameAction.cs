namespace KnightFight.Game.Actions;

/// <summary>
/// Actions a challenger/fighter can perform in a turn.
/// </summary>
public enum GameAction
{
    /// <summary>
    /// Wait and do nothing.
    /// </summary>
    Wait = 0,

    /// <summary>
    /// Move forward (direction and distance modified by active bonuses like Horse or Reverse).
    /// </summary>
    Move = 1,

    /// <summary>
    /// Turn 90 degrees counter-clockwise (left).
    /// </summary>
    TurnLeft = 2,

    /// <summary>
    /// Turn 90 degrees clockwise (right).
    /// </summary>
    TurnRight = 3,

    /// <summary>
    /// Attack using the main weapon (Sword, Spear, or Bow).
    /// </summary>
    HitMain = 4,

    /// <summary>
    /// Activate offhand item (Shield, Hammer, or Grappling Hook).
    /// </summary>
    HitOffhand = 5
}
