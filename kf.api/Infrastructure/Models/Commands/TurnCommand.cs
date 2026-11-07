namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Represents a turn/rotation command
/// </summary>
public sealed record TurnCommand(
    string ChallengerName,
    string OtpCode,
    TurnDirection Direction
) : GameCommandBase(Normalize(ChallengerName), Normalize(OtpCode))
{
    public override CommandCategory Category => CommandCategory.Turn;

    public override string ToStringCommand() => $"turn {Direction.ToString().ToLower()}";

    public override bool Validate() =>
        !string.IsNullOrWhiteSpace(ChallengerName) &&
        !string.IsNullOrWhiteSpace(OtpCode) &&
        Direction is TurnDirection.Left or TurnDirection.Right;

    public static TurnCommand? FromString(string direction, string challengerName, string otpCode)
    {
        if (Enum.TryParse<TurnDirection>(direction, true, out var dir))
        {
            return new TurnCommand(challengerName, otpCode, dir);
        }
        return null;
    }
}
