using KnightFight.Game.Actions;

namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Represents a movement command in the game (readme spec: move — move forward)
/// </summary>
public sealed record MovementCommand(
    string ChallengerName,
    string OtpCode
) : GameCommandBase(Normalize(ChallengerName), Normalize(OtpCode))
{
    public override CommandCategory Category => CommandCategory.Movement;

    public override string ToStringCommand() => "move";

    public override bool Validate() =>
        !string.IsNullOrWhiteSpace(ChallengerName) &&
        !string.IsNullOrWhiteSpace(OtpCode);

    public override GameAction ToGameAction() => GameAction.Move;
}
