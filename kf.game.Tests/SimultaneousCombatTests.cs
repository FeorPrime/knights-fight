using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Engine;
using KnightFight.Game.Management;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Tests;

public class SimultaneousCombatTests
{
    [Fact]
    public void SimultaneousAttacks_BothDealDamage_AndCanDraw()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Spear
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        // F1 at (5, 5) facing East, F2 at (6, 5) facing West
        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;
        game.Fighter1.HitPoints = 1;

        game.Fighter2.Position = new Position(6, 5);
        game.Fighter2.Direction = Direction.West;
        game.Fighter2.HitPoints = 1;

        // Both attack each other with Spear on the same turn
        game.EnqueueAction("f1", GameAction.HitMain);
        game.EnqueueAction("f2", GameAction.HitMain);

        var result = game.CalculateTurn();

        Assert.Equal(0, game.Fighter1.HitPoints);
        Assert.Equal(0, game.Fighter2.HitPoints);
        Assert.False(game.Fighter1.IsAlive);
        Assert.False(game.Fighter2.IsAlive);
        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(GameWinner.Draw, game.Winner);
    }

    [Fact]
    public void Shield_DoesNotBlockFlankOrBackAttacks()
    {
        var rand = new DeterministicRandomProvider
        {
            MainWeaponToReturn = MainWeapon.Sword,
            OffhandItemToReturn = OffhandItem.Shield
        };
        var game = new GameSession("f1", "f2", randomProvider: rand);

        // F1 at (5, 5) facing East attacking (6, 5)
        game.Fighter1.Position = new Position(5, 5);
        game.Fighter1.Direction = Direction.East;

        // F2 at (6, 5) is facing North (their back/flank is exposed to East attack from (5,5))
        game.Fighter2.Position = new Position(6, 5);
        game.Fighter2.Direction = Direction.North;
        game.Fighter2.HitPoints = 3;

        game.EnqueueAction("f1", GameAction.HitMain);
        game.EnqueueAction("f2", GameAction.HitOffhand); // F2 raises shield facing North

        var result = game.CalculateTurn();

        // Attack was NOT blocked because shield only blocks front
        Assert.Equal(2, game.Fighter2.HitPoints);
        Assert.Contains(result.Events, e => e.EventType == "FighterDamaged");
    }
}

public class ConcurrentGameManagerTests
{
    [Fact]
    public void ConcurrentOperations_AreThreadSafe()
    {
        var manager = new GameManager();
        const int gameCount = 50;
        var gameIds = new List<Guid>();

        Parallel.For(0, gameCount, i =>
        {
            var g = manager.CreateGame($"p1_{i}", $"p2_{i}");
            lock (gameIds)
            {
                gameIds.Add(g.Id);
            }
        });

        Assert.Equal(gameCount, manager.GetAllGames().Count);

        Parallel.ForEach(gameIds, id =>
        {
            var game = manager.GetGame(id);
            Assert.NotNull(game);

            manager.EnqueueAction(id, game.Fighter1.Id, GameAction.Move);
            manager.EnqueueAction(id, game.Fighter2.Id, GameAction.TurnLeft);

            var result = manager.TickGame(id);
            Assert.NotNull(result);
            Assert.Equal(1, result.TurnNumber);
        });

        Assert.Equal(gameCount, manager.GetActiveGames().Count);
    }
}
