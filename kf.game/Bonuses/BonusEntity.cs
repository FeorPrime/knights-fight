using KnightFight.Game.Common;

namespace KnightFight.Game.Bonuses;

/// <summary>
/// Represents a bonus placed on the battlefield.
/// Can be collected by fighters or hit/damaged to destroy it.
/// </summary>
public sealed class BonusEntity
{
    public Guid Id { get; }
    public BonusType Type { get; }
    public Position Position { get; set; }
    public int HitPoints { get; set; }
    public bool IsCollected { get; set; }
    public bool IsDestroyed => HitPoints <= 0 && !IsCollected;
    public bool IsPresentOnField => HitPoints > 0 && !IsCollected;

    public BonusEntity(BonusType type, Position position, int hitPoints = 1, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Type = type;
        Position = position;
        HitPoints = Math.Max(1, hitPoints);
        IsCollected = false;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || !IsPresentOnField) return;
        HitPoints = Math.Max(0, HitPoints - damage);
    }
}
