using KnightFight.Game.Management;

namespace KnightFight.Game.Tests;

public class MatchmakerTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMatchmaker _matchmaker = new Matchmaker();

    [Fact]
    public void PairUp_NoCandidates_ReturnsNoPairs()
    {
        Assert.Empty(_matchmaker.PairUp(Array.Empty<MatchCandidate>()));
    }

    [Fact]
    public void PairUp_OneCandidate_ReturnsNoPairs()
    {
        var pairs = _matchmaker.PairUp(new[] { Candidate("Alice", T0) });

        Assert.Empty(pairs);
    }

    [Fact]
    public void PairUp_TwoCandidates_PairsThem()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Alice", T0),
            Candidate("Bob", T0.AddSeconds(1))
        });

        var pair = Assert.Single(pairs);
        Assert.Equal("Alice", pair.First.Id);
        Assert.Equal("Bob", pair.Second.Id);
    }

    [Fact]
    public void PairUp_ThreeCandidates_PairsTheTwoOldestAndLeavesTheNewestWaiting()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Alice", T0),
            Candidate("Bob", T0.AddSeconds(1)),
            Candidate("Carol", T0.AddSeconds(2))
        });

        var pair = Assert.Single(pairs);
        Assert.Equal("Alice", pair.First.Id);
        Assert.Equal("Bob", pair.Second.Id);
    }

    [Fact]
    public void PairUp_FourCandidates_CreatesTwoGames()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Alice", T0),
            Candidate("Bob", T0.AddSeconds(1)),
            Candidate("Carol", T0.AddSeconds(2)),
            Candidate("Dave", T0.AddSeconds(3))
        });

        Assert.Equal(2, pairs.Count);
        Assert.Equal("Alice", pairs[0].First.Id);
        Assert.Equal("Bob", pairs[0].Second.Id);
        Assert.Equal("Carol", pairs[1].First.Id);
        Assert.Equal("Dave", pairs[1].Second.Id);
    }

    [Fact]
    public void PairUp_IgnoresTheOrderOfTheInput()
    {
        var ordered = new[] { Candidate("Alice", T0), Candidate("Bob", T0.AddSeconds(1)) };
        var shuffled = new[] { Candidate("Bob", T0.AddSeconds(1)), Candidate("Alice", T0) };

        var fromOrdered = _matchmaker.PairUp(ordered);
        var fromShuffled = _matchmaker.PairUp(shuffled);

        Assert.Equal(fromOrdered[0].First.Id, fromShuffled[0].First.Id);
        Assert.Equal(fromOrdered[0].Second.Id, fromShuffled[0].Second.Id);
    }

    [Fact]
    public void PairUp_SameReadyTime_BreaksTheTieById()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Bob", T0),
            Candidate("Alice", T0)
        });

        var pair = Assert.Single(pairs);
        Assert.Equal("Alice", pair.First.Id);
        Assert.Equal("Bob", pair.Second.Id);
    }

    private static MatchCandidate Candidate(string id, DateTime readySince) =>
        new(id, id, readySince);
}
