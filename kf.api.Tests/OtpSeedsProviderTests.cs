using KnightFight.Api.Infrastructure.Services;
using Xunit;

namespace KnightFight.Api.Tests;

/// <summary>
/// Behaviour tests for <see cref="OtpSeedsProvider"/>.
/// <para>
/// IMPORTANT: in production this service is registered as a SINGLETON
/// (see <c>Program.cs</c>: <c>AddSingleton&lt;IOtpSeedsProvider, OtpSeedsProvider&gt;()</c>),
/// so a single instance holds the seed store for the whole application lifetime and every
/// request shares it.
/// </para>
/// <para>
/// Because of that, each test in this class works on its OWN instance (xUnit creates a new
/// test-class instance - and therefore a new provider - for every test method). Sharing one
/// instance across tests would leak seeds between them and make the suite order dependent,
/// which is exactly the trap a singleton stateful service sets. The singleton semantics
/// themselves are covered explicitly in <see cref="OtpSeedsProviderSingletonTests"/>.
/// </para>
/// </summary>
public class OtpSeedsProviderTests
{
    private const string CHALLENGER_NOT_FOUND = "Challenger not found";

    /// <summary>
    /// A fresh provider per test method - never shared, so seeds never leak between tests.
    /// </summary>
    private readonly IOtpSeedsProvider _provider = new OtpSeedsProvider();

    [Fact]
    public async Task ForceAdd_NewUser_StoresSeedAndReturnsSuccess()
    {
        var result = await _provider.ForceAdd("player-1", "JBSWY3DPEHPK3PXP");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
        Assert.True(result.ResultValue);
        Assert.Empty(result.Problem);

        var stored = await _provider.Get("player-1");
        Assert.True(stored.IsSuccess);
        Assert.Equal("JBSWY3DPEHPK3PXP", stored.ResultValue);
    }

    [Fact]
    public async Task ForceAdd_ExistingUser_OverwritesThePreviousSeed()
    {
        await _provider.ForceAdd("player-1", "FIRSTSEED");
        var second = await _provider.ForceAdd("player-1", "SECONDSEED");

        Assert.True(second.IsSuccess);

        var stored = await _provider.Get("player-1");
        Assert.True(stored.IsSuccess);
        Assert.Equal("SECONDSEED", stored.ResultValue);
    }

    [Fact]
    public async Task ForceAdd_DifferentUsers_KeepsSeedsSeparate()
    {
        await _provider.ForceAdd("player-1", "SEED-ONE");
        await _provider.ForceAdd("player-2", "SEED-TWO");

        var first = await _provider.Get("player-1");
        var second = await _provider.Get("player-2");

        Assert.Equal("SEED-ONE", first.ResultValue);
        Assert.Equal("SEED-TWO", second.ResultValue);
    }

    [Fact]
    public async Task Get_UnknownUser_ReturnsFailureWithChallengerNotFound()
    {
        var result = await _provider.Get("nobody");

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Equal(CHALLENGER_NOT_FOUND, result.Problem);
        Assert.Null(result.ResultValue);
    }

    [Fact]
    public async Task Remove_ExistingUser_RemovesSeedAndReturnsSuccess()
    {
        await _provider.ForceAdd("player-1", "JBSWY3DPEHPK3PXP");

        var removed = await _provider.Remove("player-1");

        Assert.True(removed.IsSuccess);
        Assert.True(removed.ResultValue);
        Assert.Empty(removed.Problem);

        var stored = await _provider.Get("player-1");
        Assert.True(stored.IsError);
        Assert.Equal(CHALLENGER_NOT_FOUND, stored.Problem);
    }

    [Fact]
    public async Task Remove_UnknownUser_ReturnsFailureWithChallengerNotFound()
    {
        var result = await _provider.Remove("nobody");

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Equal(CHALLENGER_NOT_FOUND, result.Problem);
        // A failed bool result carries the default value (false) - it is not null.
        Assert.False(result.ResultValue);
    }

    [Fact]
    public async Task Remove_CalledTwice_SecondCallFails()
    {
        await _provider.ForceAdd("player-1", "JBSWY3DPEHPK3PXP");

        var first = await _provider.Remove("player-1");
        var second = await _provider.Remove("player-1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsError);
        Assert.Equal(CHALLENGER_NOT_FOUND, second.Problem);
    }

    [Fact]
    public async Task ForceAdd_NullUserId_ReturnsFailureInsteadOfThrowing()
    {
        var result = await _provider.ForceAdd(null!, "JBSWY3DPEHPK3PXP");

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Problem));
        Assert.Contains("key", result.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailureResult_ForBoolPayload_CarriesTheDefaultFalseValue()
    {
        // ExceptResult<bool> stores the payload as a value type, so a failure does not return
        // null - it returns false. Callers must check IsError, not the value.
        var removed = await _provider.Remove("nobody");
        var added = await _provider.ForceAdd(null!, "JBSWY3DPEHPK3PXP");

        Assert.True(removed.IsError);
        Assert.False(removed.ResultValue);
        Assert.True(added.IsError);
        Assert.False(added.ResultValue);
    }

    [Fact]
    public async Task ForceAdd_NullSeed_ReportsSuccessButStoresNothingUsable()
    {
        // Documents the current behaviour: the dictionary accepts a null value, so the call
        // "succeeds" while Get hands back a successful result with no seed inside.
        var added = await _provider.ForceAdd("player-1", null!);

        Assert.True(added.IsSuccess);

        var stored = await _provider.Get("player-1");
        Assert.True(stored.IsSuccess);
        Assert.Null(stored.ResultValue);
    }

    [Fact]
    public async Task ForceAdd_EmptyUserId_IsAcceptedAsAValidKey()
    {
        // Documents the current behaviour: only a null key is rejected, there is no validation.
        var added = await _provider.ForceAdd(string.Empty, "JBSWY3DPEHPK3PXP");

        Assert.True(added.IsSuccess);

        var stored = await _provider.Get(string.Empty);
        Assert.True(stored.IsSuccess);
        Assert.Equal("JBSWY3DPEHPK3PXP", stored.ResultValue);
    }

    [Fact]
    public async Task Get_AfterRemoveOfAnotherUser_StillReturnsOwnSeed()
    {
        await _provider.ForceAdd("player-1", "SEED-ONE");
        await _provider.ForceAdd("player-2", "SEED-TWO");

        await _provider.Remove("player-1");

        var stored = await _provider.Get("player-2");
        Assert.True(stored.IsSuccess);
        Assert.Equal("SEED-TWO", stored.ResultValue);
    }

    [Fact]
    public async Task ForceAdd_FromManyThreads_AllSeedsAreStored()
    {
        const int users = 500;

        // Task.Run is required: the provider methods are synchronous under the hood
        // (they return Task.FromResult), so a plain Task.WhenAll would run them one after
        // another on the calling thread and never exercise any concurrency at all.
        var results = await Task.WhenAll(Enumerable.Range(0, users)
            .Select(i => Task.Run(() => _provider.ForceAdd($"player-{i}", $"SEED-{i}"))));

        Assert.All(results, r => Assert.True(r.IsSuccess));

        var reads = await Task.WhenAll(Enumerable.Range(0, users)
            .Select(i => Task.Run(() => _provider.Get($"player-{i}"))));
        Assert.All(reads, r => Assert.True(r.IsSuccess));
        for (var i = 0; i < users; i++)
        {
            Assert.Equal($"SEED-{i}", reads[i].ResultValue);
        }
    }

    [Fact]
    public async Task ForceAdd_SameUserFromManyThreads_KeepsOneValidSeed()
    {
        const int writes = 500;

        var results = await Task.WhenAll(Enumerable.Range(0, writes)
            .Select(i => Task.Run(() => _provider.ForceAdd("player-1", $"SEED-{i}"))));

        Assert.All(results, r => Assert.True(r.IsSuccess));

        var stored = await _provider.Get("player-1");
        Assert.True(stored.IsSuccess);
        Assert.Contains(stored.ResultValue, Enumerable.Range(0, writes).Select(i => $"SEED-{i}"));
    }

    [Fact]
    public async Task Remove_WhileOtherOperationsRun_NeverThrows()
    {
        await _provider.ForceAdd("player-1", "SEED-ONE");

        // Task.Run keeps the operations genuinely parallel - see the note in
        // ForceAdd_FromManyThreads_AllSeedsAreStored.
        var work = new List<Task>();
        for (var i = 0; i < 100; i++)
        {
            work.Add(Task.Run(() => _provider.ForceAdd($"player-{i}", $"SEED-{i}")));
            work.Add(Task.Run(() => _provider.Get("player-1")));
            work.Add(Task.Run(() => _provider.Remove("player-1")));
        }

        await Task.WhenAll(work);

        // Whatever the interleaving was, the store must still be usable.
        var readded = await _provider.ForceAdd("player-1", "SEED-FINAL");
        Assert.True(readded.IsSuccess);
        var stored = await _provider.Get("player-1");
        Assert.True(stored.IsSuccess);
        Assert.Equal("SEED-FINAL", stored.ResultValue);
    }

    [Fact]
    public async Task ForceAdd_PutsUserInLobbyWithUnreadyStatus()
    {
        var provider = new OtpSeedsProvider();
        await provider.ForceAdd("Alice", "SEED-ALICE");

        var lobbyUsers = await provider.GetLobbyUsers();
        var alice = Assert.Single(lobbyUsers);
        Assert.Equal("Alice", alice.Name);
        Assert.False(alice.IsReady);
        Assert.Equal("unready", alice.Status);
    }

    [Fact]
    public async Task SetReady_ChangesStatusToReadyAndUnready()
    {
        var provider = new OtpSeedsProvider();
        await provider.ForceAdd("Alice", "SEED-ALICE");

        var setReadyResult = await provider.SetReady("Alice", true);
        Assert.True(setReadyResult.IsSuccess);

        var usersAfterReady = await provider.GetLobbyUsers();
        var readyUser = Assert.Single(usersAfterReady);
        Assert.True(readyUser.IsReady);
        Assert.Equal("ready", readyUser.Status);

        var setUnreadyResult = await provider.SetReady("Alice", false);
        Assert.True(setUnreadyResult.IsSuccess);

        var usersAfterUnready = await provider.GetLobbyUsers();
        var unreadyUser = Assert.Single(usersAfterUnready);
        Assert.False(unreadyUser.IsReady);
        Assert.Equal("unready", unreadyUser.Status);
    }

    [Fact]
    public async Task Inactivity_MoreThanOneMinute_RemovesUserFromLobby()
    {
        var timeProvider = new SettableTimeProvider();
        var provider = new OtpSeedsProvider(TimeSpan.FromMinutes(1), timeProvider);

        await provider.ForceAdd("Alice", "SEED-ALICE");

        // Within 1 minute (e.g. 30 seconds), user is still present
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        var usersBeforeTimeout = await provider.GetLobbyUsers();
        Assert.Single(usersBeforeTimeout);

        // Advance beyond 1 minute total
        timeProvider.Advance(TimeSpan.FromSeconds(35)); // total 65s since last active
        var usersAfterTimeout = await provider.GetLobbyUsers();
        Assert.Empty(usersAfterTimeout);

        // Get and SetReady also fail because session is expired and removed
        var getResult = await provider.Get("Alice");
        Assert.True(getResult.IsError);

        var setReadyResult = await provider.SetReady("Alice", true);
        Assert.True(setReadyResult.IsError);
    }

    [Fact]
    public async Task Activity_RefreshesTimeoutAndKeepsUserInLobby()
    {
        var timeProvider = new SettableTimeProvider();
        var provider = new OtpSeedsProvider(TimeSpan.FromMinutes(1), timeProvider);

        await provider.ForceAdd("Alice", "SEED-ALICE");

        // Advance 45s (not expired yet)
        timeProvider.Advance(TimeSpan.FromSeconds(45));

        // User performs an action (e.g. Get / send command / touch / set ready)
        var touchResult = await provider.Touch("Alice");
        Assert.True(touchResult.IsSuccess);

        // Advance another 45s (total 90s from start, but only 45s from last touch)
        timeProvider.Advance(TimeSpan.FromSeconds(45));

        var users = await provider.GetLobbyUsers();
        var alice = Assert.Single(users);
        Assert.Equal("Alice", alice.Name);
    }

    [Fact]
    public async Task SetReady_FirstTime_StampsTheReadyMoment()
    {
        var provider = new OtpSeedsProvider();

        await provider.ForceAdd("Alice", "SEED-ALICE");
        await provider.SetReady("Alice", true);

        var users = await provider.GetLobbyUsers();
        var alice = Assert.Single(users);
        Assert.True(alice.IsReady);
        Assert.NotNull(alice.ReadySince);
    }

    [Fact]
    public async Task SetReady_CalledAgain_KeepsTheOriginalReadyMoment()
    {
        var clock = new SettableTimeProvider();
        var provider = new OtpSeedsProvider(TimeSpan.FromMinutes(5), clock);

        await provider.ForceAdd("Alice", "SEED-ALICE");
        await provider.SetReady("Alice", true);

        var firstStamp = (await provider.GetLobbyUsers()).Single().ReadySince;

        clock.Advance(TimeSpan.FromSeconds(30));
        await provider.SetReady("Alice", true);

        var secondStamp = (await provider.GetLobbyUsers()).Single().ReadySince;
        Assert.Equal(firstStamp, secondStamp);
    }

    [Fact]
    public async Task SetReady_False_ClearsTheReadyMoment()
    {
        var provider = new OtpSeedsProvider();

        await provider.ForceAdd("Alice", "SEED-ALICE");
        await provider.SetReady("Alice", true);
        await provider.SetReady("Alice", false);

        var users = await provider.GetLobbyUsers();
        var alice = Assert.Single(users);
        Assert.False(alice.IsReady);
        Assert.Null(alice.ReadySince);
    }

    private class SettableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
