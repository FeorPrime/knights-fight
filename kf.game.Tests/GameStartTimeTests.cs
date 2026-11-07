using KnightFight.Game.Management;

namespace KnightFight.Game.Tests;

public class GameStartTimeTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Game_StartedAt_ComesFromTheInjectedTimeProvider()
    {
        var clock = new TestTimeProvider(FixedNow);

        var game = new GameSession("alice", "bob", timeProvider: clock);

        Assert.Equal(new DateTime(2026, 11, 7, 12, 0, 0), game.StartedAt);
    }

    [Fact]
    public void GameManager_ForwardsItsTimeProviderToTheGameItCreates()
    {
        var clock = new TestTimeProvider(FixedNow);
        var manager = new GameManager(clock);

        var game = manager.CreateGame("alice", "bob");

        Assert.Equal(new DateTime(2026, 11, 7, 12, 0, 0), game.StartedAt);
    }
}
