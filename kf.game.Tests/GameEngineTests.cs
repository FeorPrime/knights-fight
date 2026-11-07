using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Engine;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Tests;

public sealed class DeterministicRandomProvider : IRandomProvider
{
    public MainWeapon MainWeaponToReturn { get; set; } = MainWeapon.Sword;
    public OffhandItem OffhandItemToReturn { get; set; } = OffhandItem.Shield;
    public BonusType BonusTypeToReturn { get; set; } = BonusType.Heart;

    public MainWeapon NextMainWeapon() => MainWeaponToReturn;
    public OffhandItem NextOffhandItem() => OffhandItemToReturn;
    public BonusType NextBonusType() => BonusTypeToReturn;
    public int NextInt(int minValue, int maxValue) => minValue;
}

public class GameInitializationTests
{
    [Fact]
    public void Game_InitializesWithCorrectDefaultRulesAndPositions()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Spear,
            OffhandItemToReturn = OffhandItem.Hammer,
            BonusTypeToReturn = BonusType.Horse
        };

        var game = new GameSession("alice", "bob", "Alice", "Bob", randomProvider: rand);

        Assert.Equal(0, game.TurnNumber);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(GameWinner.None, game.Winner);
        Assert.Equal(new BoardBounds(0, 19, 0, 19), game.Bounds);

        // Fighter 1 at (1, 1) with 3 HP, facing East
        Assert.Equal(new Position(1, 1), game.Fighter1.Position);
        Assert.Equal(Direction.East, game.Fighter1.Direction);
        Assert.Equal(3, game.Fighter1.HitPoints);
        Assert.Equal(MainWeapon.Spear, game.Fighter1.MainWeapon);
        Assert.Equal(OffhandItem.Hammer, game.Fighter1.OffhandItem);

        // Fighter 2 at (18, 18) with 3 HP, facing West
        Assert.Equal(new Position(18, 18), game.Fighter2.Position);
        Assert.Equal(Direction.West, game.Fighter2.Direction);
        Assert.Equal(3, game.Fighter2.HitPoints);

        // Bonuses at (5, 14) and (14, 5)
        Assert.Equal(2, game.Bonuses.Count);
        Assert.Contains(game.Bonuses, b => b.Position == new Position(5, 14) && b.Type == BonusType.Horse);
        Assert.Contains(game.Bonuses, b => b.Position == new Position(14, 5) && b.Type == BonusType.Horse);
    }
}

public class ActionQueueTests
{
    [Fact]
    public void Fighter_StoresMax3BufferedMoves_AndPopsOnePerTurn()
    {
        var game = new GameSession("c1", "c2");

        Assert.True(game.EnqueueAction("c1", GameAction.Move));
        Assert.True(game.EnqueueAction("c1", GameAction.TurnLeft));
        Assert.True(game.EnqueueAction("c1", GameAction.HitMain));

        // 4th move should be rejected
        Assert.False(game.EnqueueAction("c1", GameAction.HitOffhand));

        Assert.Equal(3, game.Fighter1.BufferedMovesCount);

        var result1 = game.CalculateTurn();
        Assert.Equal(1, result1.TurnNumber);
        Assert.Equal(GameAction.Move, result1.Fighter1Action);
        Assert.Equal(GameAction.Wait, result1.Fighter2Action); // c2 had no buffered moves
        Assert.Equal(2, game.Fighter1.BufferedMovesCount);

        var result2 = game.CalculateTurn();
        Assert.Equal(GameAction.TurnLeft, result2.Fighter1Action);
        Assert.Equal(1, game.Fighter1.BufferedMovesCount);

        var result3 = game.CalculateTurn();
        Assert.Equal(GameAction.HitMain, result3.Fighter1Action);
        Assert.Equal(0, game.Fighter1.BufferedMovesCount);

        var result4 = game.CalculateTurn();
        Assert.Equal(GameAction.Wait, result4.Fighter1Action);
    }
}
