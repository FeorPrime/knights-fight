using KnightFight.Game.Bonuses;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Engine;

/// <summary>
/// Abstraction for random generation to allow deterministic testing and replayability.
/// </summary>
public interface IRandomProvider
{
    MainWeapon NextMainWeapon();
    OffhandItem NextOffhandItem();
    BonusType NextBonusType();
    int NextInt(int minValue, int maxValue);
}

public sealed class DefaultRandomProvider : IRandomProvider
{
    private readonly Random _random;

    public DefaultRandomProvider(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    public MainWeapon NextMainWeapon()
    {
        var values = Enum.GetValues<MainWeapon>();
        return values[_random.Next(values.Length)];
    }

    public OffhandItem NextOffhandItem()
    {
        var values = Enum.GetValues<OffhandItem>();
        return values[_random.Next(values.Length)];
    }

    public BonusType NextBonusType()
    {
        var values = Enum.GetValues<BonusType>();
        return values[_random.Next(values.Length)];
    }

    public int NextInt(int minValue, int maxValue) => _random.Next(minValue, maxValue);
}
