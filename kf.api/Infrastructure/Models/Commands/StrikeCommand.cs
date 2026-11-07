using KnightFight.Game.Actions;

namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Represents a strike command in the game (readme spec: hit_main / hit_off)
/// </summary>
public sealed record StrikeCommand(
    string ChallengerName,
    string OtpCode,
    StrikeSide Side
) : GameCommandBase(Normalize(ChallengerName), Normalize(OtpCode))
{
    public override CommandCategory Category => CommandCategory.Attack;

    public override string ToStringCommand() => $"hit_{Side.ToString().ToLower()}";

    public override bool Validate() =>
        !string.IsNullOrWhiteSpace(ChallengerName) &&
        !string.IsNullOrWhiteSpace(OtpCode) &&
        Side is StrikeSide.Main or StrikeSide.Off;

    public override GameAction ToGameAction() =>
        Side == StrikeSide.Main ? GameAction.HitMain : GameAction.HitOffhand;

    // Game-dev: Allow creation from string for parsing. The enum members are spelled
    // like the readme verbs (main / off), same as TurnDirection matches "turn left".
    public static StrikeCommand? FromString(string side, string challengerName, string otpCode)
    {
        if (Enum.TryParse<StrikeSide>(side, true, out var strike))
        {
            return new StrikeCommand(challengerName, otpCode, strike);
        }

        return null;
    }
}
