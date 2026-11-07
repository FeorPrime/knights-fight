namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// A match that has just been created.
/// </summary>
public sealed record StartedMatch(
    Guid Id,
    string Challenger1,
    string Challenger2,
    string Status,
    DateTime StartedAt);

/// <summary>
/// The result of a match-start attempt.
/// </summary>
public sealed record MatchStartOutcome(IReadOnlyList<StartedMatch> StartedMatches)
{
    /// <summary>
    /// The match the given player was placed in, or null when they are still waiting.
    /// </summary>
    public StartedMatch? ForPlayer(string playerName) =>
        StartedMatches.FirstOrDefault(m =>
            string.Equals(m.Challenger1, playerName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Challenger2, playerName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The opponent of the given player, or null when they are still waiting.
    /// </summary>
    public string? OpponentFor(string playerName) => ForPlayer(playerName) switch
    {
        null => null,
        var match when string.Equals(match.Challenger1, playerName, StringComparison.OrdinalIgnoreCase)
            => match.Challenger2,
        var match => match.Challenger1
    };
}

/// <summary>
/// Turns the ready challengers in the lobby into running matches.
/// </summary>
public interface ILobbyMatchService
{
    /// <summary>
    /// Pairs every ready challenger into a match and starts it. Challengers without a
    /// partner keep waiting.
    /// </summary>
    Task<MatchStartOutcome> TryStartMatchesAsync();
}
