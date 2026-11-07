using System.Text.RegularExpressions;

namespace KnightFight.Api.Infrastructure.Models.Commands;

/// <summary>
/// Parses string commands into strongly-typed command objects
/// Uses strategy pattern for extensibility
/// </summary>
public static class CommandParser
{
    // Game-dev best practice: Use regex for robust parsing
    private static readonly Dictionary<CommandCategory, Regex> CommandPatterns = new()
    {
        [CommandCategory.Movement] = new Regex(
            @"^move$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        ),
        [CommandCategory.Attack] = new Regex(
            @"^hit_(main|off)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        ),
        [CommandCategory.Turn] = new Regex(
            @"^turn\s+(left|right)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        )
    };

    private static readonly Dictionary<CommandCategory, Func<Match, string, string, IGameCommand>> FactoryMethods =
        new()
        {
            [CommandCategory.Movement] = (match, name, otp) =>
                new MovementCommand(name, otp),
            [CommandCategory.Attack] = (match, name, otp) =>
                StrikeCommand.FromString(match.Groups[1].Value, name, otp)!,
            [CommandCategory.Turn] = (match, name, otp) =>
                TurnCommand.FromString(match.Groups[1].Value, name, otp)!
        };

    /// <summary>
    /// Parse a command string into a strongly-typed command
    /// </summary>
    public static IGameCommand? Parse(string commandText, string challengerName, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return null;

        var normalizedCommand = commandText.Trim();

        foreach (var (category, pattern) in CommandPatterns)
        {
            var match = pattern.Match(normalizedCommand);
            if (match.Success)
            {
                var command = FactoryMethods[category](match, challengerName, otpCode);
                if (command.Validate())
                {
                    return command;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Try to parse with detailed error information
    /// </summary>
    public static (IGameCommand? Command, string Error)? TryParseWithError(
        string commandText, string challengerName, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return (null, "Command text is empty");

        if (string.IsNullOrWhiteSpace(challengerName))
            return (null, "Challenger name is required");

        if (string.IsNullOrWhiteSpace(otpCode))
            return (null, "OTP code is required");

        var command = Parse(commandText, challengerName, otpCode);
        if (command == null)
            return (null, string.Format("Invalid command format: '{0}'", commandText));

        return (command, string.Empty);
    }
}
