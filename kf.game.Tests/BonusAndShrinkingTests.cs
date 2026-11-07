using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Engine;
using KnightFight.Game.Management;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Tests;

public class BonusAndShrinkingTests
{
    [Fact]
    public void Bonus_Heart_IncreasesHp()
    {
        var rand = new DeterministicRandomProvider { BonusTypeToReturn = BonusType.Heart };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        // Position F1 on Bonus 1 at (5, 14)
        game.Fighter1.Position = new Position(4, 14);
        game.Fighter1.Direction = Direction.East;

        game.EnqueueAction("f1", GameAction.Move);
        game.CalculateTurn();

        Assert.Equal(new Position(5, 14), game.Fighter1.Position);
        Assert.Equal(4, game.Fighter1.HitPoints); // 3 + 1 = 4
        Assert.True(game.Bonuses[0].IsCollected);
    }

    [Fact]
    public void Bonus_Tp_SwapsPositions()
    {
        var rand = new DeterministicRandomProvider { BonusTypeToReturn = BonusType.Tp };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        game.Fighter1.Position = new Position(4, 14);
        game.Fighter1.Direction = Direction.East;
        game.Fighter2.Position = new Position(10, 10);

        game.EnqueueAction("f1", GameAction.Move);
        game.CalculateTurn();

        // F1 moved to (5,14) which triggers TP, swapping with F2 at (10,10)
        Assert.Equal(new Position(10, 10), game.Fighter1.Position);
        Assert.Equal(new Position(5, 14), game.Fighter2.Position);
    }

    [Fact]
    public void Bonus_BoltAndStorm_DealsDamageCorrectly()
    {
        // Test Bolt
        var randBolt = new DeterministicRandomProvider { BonusTypeToReturn = BonusType.Bolt };
        var gameBolt = new GameSession("f1", "f2", randomProvider: randBolt);

        gameBolt.Fighter1.Position = new Position(4, 14);
        gameBolt.Fighter1.Direction = Direction.East;
        gameBolt.EnqueueAction("f1", GameAction.Move);
        gameBolt.CalculateTurn();

        Assert.Equal(3, gameBolt.Fighter1.HitPoints);
        Assert.Equal(2, gameBolt.Fighter2.HitPoints); // took 1 damage from bolt

        // Test Storm
        var randStorm = new DeterministicRandomProvider { BonusTypeToReturn = BonusType.Storm };
        var gameStorm = new GameSession("f1", "f2", randomProvider: randStorm);

        gameStorm.Fighter1.Position = new Position(4, 14);
        gameStorm.Fighter1.Direction = Direction.East;
        gameStorm.EnqueueAction("f1", GameAction.Move);
        gameStorm.CalculateTurn();

        Assert.Equal(2, gameStorm.Fighter1.HitPoints); // both took 1 damage
        Assert.Equal(2, gameStorm.Fighter2.HitPoints);
    }

    [Fact]
    public void Bonus_CanBeHitAndDestroyed()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Sword
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        // Place F1 at (4, 14) facing East towards bonus at (5, 14)
        game.Fighter1.Position = new Position(4, 14);
        game.Fighter1.Direction = Direction.East;

        var bonus = game.Bonuses.First(b => b.Position == new Position(5, 14));
        Assert.Equal(1, bonus.HitPoints);
        Assert.False(bonus.IsDestroyed);

        game.EnqueueAction("f1", GameAction.HitMain);
        game.CalculateTurn();

        Assert.Equal(0, bonus.HitPoints);
        Assert.True(bonus.IsDestroyed);
    }

    [Fact]
    public void Ring_PushesAdjacentFighterAwayAtEndOfTurn()
    {
        var game = new GameSession("f1", "f2");
        game.Fighter1.AddBonus(BonusType.Ring);
        game.Fighter1.Position = new Position(5, 5);

        // F2 is adjacent at (6, 5)
        game.Fighter2.Position = new Position(6, 5);

        game.CalculateTurn();

        // F2 pushed 1 square away to (7, 5)
        Assert.Equal(new Position(7, 5), game.Fighter2.Position);
    }

    [Fact]
    public void Board_ShrinksEvery5Turns_AndDealsOutOfBoundsDamage()
    {
        var game = new GameSession("f1", "f2");

        Assert.Equal(new BoardBounds(0, 19, 0, 19), game.Bounds);

        // 4 turns: bounds unchanged
        for (int i = 0; i < 4; i++)
        {
            game.CalculateTurn();
            Assert.Equal(new BoardBounds(0, 19, 0, 19), game.Bounds);
        }

        // 5th turn: shrinks by 1 row and 1 col from each side -> (1, 18, 1, 18)
        var result5 = game.CalculateTurn();
        Assert.True(result5.DidBoardShrink);
        Assert.Equal(new BoardBounds(1, 18, 1, 18), game.Bounds);

        // Place F1 at (0, 0) which is now out of bounds
        game.Fighter1.Position = new Position(0, 0);
        game.Fighter1.HitPoints = 3;

        game.CalculateTurn(); // Turn 6

        // F1 should have taken 1 damage out of bounds
        Assert.Equal(2, game.Fighter1.HitPoints);

        game.CalculateTurn(); // Turn 7 -> HP 1
        Assert.Equal(1, game.Fighter1.HitPoints);

        game.CalculateTurn(); // Turn 8 -> HP 0 -> F1 dies
        Assert.Equal(0, game.Fighter1.HitPoints);
        Assert.False(game.Fighter1.IsAlive);
        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(GameWinner.Fighter2, game.Winner);
    }
}

public class GameManagerTests
{
    [Fact]
    public void GameManager_CreatesAndFindsGamesByIdAndChallenger()
    {
        var manager = new GameManager();

        var game = manager.CreateGame("alice", "bob", "Alice", "Bob");

        Assert.NotNull(game);
        Assert.Equal(game, manager.GetGame(game.Id));
        Assert.Equal(game, manager.GetGameByChallenger("alice"));
        Assert.Equal(game, manager.GetGameByChallenger("bob"));
        Assert.Equal(game, manager.GetGameByChallenger("ALICE")); // Case insensitive

        Assert.Null(manager.GetGameByChallenger("unknown"));

        Assert.Single(manager.GetActiveGames());
        Assert.Single(manager.GetAllGames());

        // Enqueue moves via manager
        Assert.True(manager.EnqueueAction(game.Id, "alice", GameAction.Move));
        Assert.Equal(1, game.Fighter1.BufferedMovesCount);

        // Tick game via manager
        var result = manager.TickGame(game.Id);
        Assert.NotNull(result);
        Assert.Equal(1, result.TurnNumber);
        Assert.Equal(GameAction.Move, result.Fighter1Action);

        // Remove game
        Assert.True(manager.RemoveGame(game.Id));
        Assert.Null(manager.GetGame(game.Id));
        Assert.Null(manager.GetGameByChallenger("alice"));
        Assert.Empty(manager.GetAllGames());
    }
}
