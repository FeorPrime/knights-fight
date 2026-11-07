using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Engine;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Tests;

public class CombatAndMovementTests
{
    [Fact]
    public void MovementAndRotations_WorkCorrectly()
    {
        var game = new GameSession("f1", "f2");
        Assert.Equal(new Position(1, 1), game.Fighter1.Position);
        Assert.Equal(Direction.East, game.Fighter1.Direction);

        // Turn right -> South
        game.EnqueueAction("f1", GameAction.TurnRight);
        game.CalculateTurn();
        Assert.Equal(Direction.South, game.Fighter1.Direction);
        Assert.Equal(new Position(1, 1), game.Fighter1.Position);

        // Move forward -> (1, 2)
        game.EnqueueAction("f1", GameAction.Move);
        game.CalculateTurn();
        Assert.Equal(new Position(1, 2), game.Fighter1.Position);

        // Horse bonus: moves 2 squares
        game.Fighter1.AddBonus(BonusType.Horse);
        game.EnqueueAction("f1", GameAction.Move);
        game.CalculateTurn();
        Assert.Equal(new Position(1, 4), game.Fighter1.Position);

        // Reverse bonus: moves backwards
        game.Fighter1.AddBonus(BonusType.Reverse);
        game.EnqueueAction("f1", GameAction.Move);
        game.CalculateTurn();
        // Facing South, moving reverse with horse = 2 squares North -> (1, 2)
        Assert.Equal(new Position(1, 2), game.Fighter1.Position);
    }

    [Fact]
    public void Sword_Swings3SquaresInFront()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Sword
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        // Position F1 at (5, 5) facing East
        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // Position F2 at (6, 4) (front-left relative to East)
        game.Fighter2.Position = new Position(6, 4);
        game.Fighter2.HitPoints = 3;

        game.EnqueueAction("f1", GameAction.HitMain);
        var result = game.CalculateTurn();

        Assert.Equal(2, game.Fighter2.HitPoints);
        Assert.Contains(result.Events, e => e.EventType == "FighterDamaged");
    }

    [Fact]
    public void Spear_Pierces2SquaresInFront()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Spear
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // F2 at distance 2 in front (7, 5)
        game.Fighter2.Position = new Position(7, 5);
        game.Fighter2.HitPoints = 3;

        game.EnqueueAction("f1", GameAction.HitMain);
        game.CalculateTurn();

        Assert.Equal(2, game.Fighter2.HitPoints);
    }

    [Fact]
    public void Bow_Skips2SquaresAndHitsNext2Squares()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Bow
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // F2 at distance 3 in front (8, 5)
        game.Fighter2.Position = new Position(8, 5);
        game.Fighter2.HitPoints = 3;

        game.EnqueueAction("f1", GameAction.HitMain);
        game.CalculateTurn();

        Assert.Equal(2, game.Fighter2.HitPoints);

        // Distance 1 should NOT be hit by Bow (skips 2)
        game.Fighter2.Position = new Position(6, 5);
        game.EnqueueAction("f1", GameAction.HitMain);
        game.CalculateTurn();

        // HP remains 2
        Assert.Equal(2, game.Fighter2.HitPoints);
    }

    [Fact]
    public void Shield_BlocksFrontalAttacks()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Sword,
            OffhandItemToReturn = OffhandItem.Shield
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        // F1 at (5, 5) facing East attacking F2 at (6, 5)
        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // F2 at (6, 5) facing West (facing F1) and raising shield
        game.Fighter2.Position = new Position(6, 5);
        game.Fighter2.Direction = Direction.West;
        game.Fighter2.HitPoints = 3;

        game.EnqueueAction("f1", GameAction.HitMain);
        game.EnqueueAction("f2", GameAction.HitOffhand);

        var result = game.CalculateTurn();

        // Damage was blocked
        Assert.Equal(3, game.Fighter2.HitPoints);
        Assert.Contains(result.Events, e => e.EventType == "AttackBlocked");
    }

    [Fact]
    public void Hammer_PushesOpponent2SquaresAway()
    {
        var rand = new DeterministicRandomProvider
        {
            OffhandItemToReturn = OffhandItem.Hammer
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // F2 is directly in front at (6, 5)
        game.Fighter2.Position = new Position(6, 5);

        game.EnqueueAction("f1", GameAction.HitOffhand);
        game.CalculateTurn();

        // F2 pushed 2 squares to (8, 5)
        Assert.Equal(new Position(8, 5), game.Fighter2.Position);
    }

    [Fact]
    public void GrapplingHook_PullsTarget1SquareCloser()
    {
        var rand = new DeterministicRandomProvider
        {
            OffhandItemToReturn = OffhandItem.GrapplingHook
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // F2 is 3 squares in front at (8, 5)
        game.Fighter2.Position = new Position(8, 5);

        game.EnqueueAction("f1", GameAction.HitOffhand);
        game.CalculateTurn();

        // F2 pulled 1 square closer to (7, 5)
        Assert.Equal(new Position(7, 5), game.Fighter2.Position);
    }
}
