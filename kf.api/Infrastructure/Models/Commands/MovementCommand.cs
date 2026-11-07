namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Represents a movement command in the game
/// </summary>
public sealed record MovementCommand(
    string ChallengerName,
    string OtpCode,
    MovementDirection Direction
) : GameCommandBase(Normalize(ChallengerName), Normalize(OtpCode))
{
    public override CommandCategory Category => CommandCategory.Movement;

    public override string ToStringCommand() => $"move {Direction.ToString().ToLower()}";

    public override bool Validate() =>
        !string.IsNullOrWhiteSpace(ChallengerName) &&
        !string.IsNullOrWhiteSpace(OtpCode) &&
        Direction is >= MovementDirection.Up and <= MovementDirection.Right;

    // Game-dev: Allow creation from string for parsing
    public static MovementCommand? FromString(string direction, string challengerName, string otpCode)
    {
        if (Enum.TryParse<MovementDirection>(direction, true, out var dir))
        {
            return new MovementCommand(challengerName, otpCode, dir);
        }
        return null;
    }
}
