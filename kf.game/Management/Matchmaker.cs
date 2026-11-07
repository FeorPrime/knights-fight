namespace KnightFight.Game.Management;

/// <summary>
/// A challenger who has declared themselves ready to fight.
/// </summary>
/// <param name="Id">Stable identifier. In the API this is the challenger name.</param>
/// <param name="Name">Display name.</param>
/// <param name="ReadySince">When the challenger declared themselves ready.</param>
public sealed record MatchCandidate(string Id, string Name, DateTime ReadySince);

/// <summary>
/// Two challengers that will fight each other.
/// </summary>
public sealed record MatchPair(MatchCandidate First, MatchCandidate Second);

/// <summary>
/// Turns a set of ready challengers into one-on-one pairs.
/// </summary>
public interface IMatchmaker
{
    /// <summary>
    /// Pairs the given candidates. The challengers that have been ready the longest are
    /// paired first. A candidate without a partner is left out of the result and keeps
    /// waiting for the next call.
    /// </summary>
    IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates);
}

/// <inheritdoc cref="IMatchmaker" />
public sealed class Matchmaker : IMatchmaker
{
    public IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count < 2)
        {
            return Array.Empty<MatchPair>();
        }

        // The ready set arrives in dictionary order, which is not stable - sort it so the
        // same lobby always produces the same pairing. The id breaks a tie on the clock.
        var ordered = candidates
            .OrderBy(c => c.ReadySince)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();

        var pairs = new List<MatchPair>(ordered.Length / 2);
        for (var i = 0; i + 1 < ordered.Length; i += 2)
        {
            pairs.Add(new MatchPair(ordered[i], ordered[i + 1]));
        }

        // An odd candidate out has no partner and stays ready for the next round.
        return pairs;
    }
}
