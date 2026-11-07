namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Represents a hand action command
/// </summary>
public sealed record HandCommand(
    string ChallengerName,
    string OtpCode,
    HandSide Hand
) : GameCommandBase(Normalize(ChallengerName), Normalize(OtpCode))
{
    public override CommandCategory Category => CommandCategory.HandAction;

    public override string ToStringCommand() => $"use hand_{Hand.ToString().ToLower()}";

    public override bool Validate() =>
        !string.IsNullOrWhiteSpace(ChallengerName) &&
        !string.IsNullOrWhiteSpace(OtpCode) &&
        Hand is HandSide.Left or HandSide.Right;

    public static HandCommand? FromString(string hand, string challengerName, string otpCode)
    {
        var normalized = hand.Replace("hand_", "").Replace(" ", "");
        if (Enum.TryParse<HandSide>(normalized, true, out var side))
        {
            return new HandCommand(challengerName, otpCode, side);
        }
        return null;
    }
}
