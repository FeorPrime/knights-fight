namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Base interface for all game commands (Command Pattern)
/// </summary>
public interface IGameCommand
{
    string ChallengerName { get; }
    string OtpCode { get; }
    CommandCategory Category { get; }
    string ToStringCommand();
    bool Validate();
}

/// <summary>
/// Base record for common command properties
/// </summary>
public abstract record GameCommandBase(
    string ChallengerName,
    string OtpCode
) : IGameCommand
{
    public abstract CommandCategory Category { get; }

    // Normalize strings for consistency (game-dev best practice).
    // Static: the derived records call it from their base-constructor arguments,
    // where an instance member would not be available yet.
    protected static string Normalize(string input) =>
        input?.Trim() ?? string.Empty;

    public abstract string ToStringCommand();
    public abstract bool Validate();

    // Implicit conversion for easy parsing
    public static implicit operator string(GameCommandBase command) =>
        command.ToStringCommand();
}
