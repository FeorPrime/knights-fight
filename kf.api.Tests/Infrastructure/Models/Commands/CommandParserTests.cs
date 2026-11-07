using KnightFight.Game.Actions;
using KnightFight.Api.Infrastructure.Models.Commands;

namespace kf.api.Tests.Infrastructure.Models.Commands;

public class CommandParserTests
{
    [Theory]
    [InlineData("move", "Player1", "123456", typeof(MovementCommand), GameAction.Move)]
    [InlineData("MOVE", "Player1", "123456", typeof(MovementCommand), GameAction.Move)]
    [InlineData(" hit_main ", "Player1", "123456", typeof(StrikeCommand), GameAction.HitMain)]
    [InlineData("HIT_OFF", "Player1", "123456", typeof(StrikeCommand), GameAction.HitOffhand)]
    [InlineData("turn left", "Player1", "123456", typeof(TurnCommand), GameAction.TurnLeft)]
    [InlineData("TURN RIGHT", "Player1", "123456", typeof(TurnCommand), GameAction.TurnRight)]
    public void Parse_SpecVerbs_ReturnsMatchingGameAction(
        string command, string name, string otp, Type expectedType, GameAction expectedAction)
    {
        var result = CommandParser.Parse(command, name, otp);

        Assert.NotNull(result);
        Assert.IsType(expectedType, result);
        Assert.True(result.Validate());
        Assert.Equal(expectedAction, result.ToGameAction());
    }

    [Theory]
    // The old grammar is gone: a bare direction or a missing side is not a verb.
    [InlineData("move up")]
    [InlineData("use hand_left")]
    [InlineData("hit_left")]
    [InlineData("hit main")]
    [InlineData("turn around")]
    [InlineData("turnleft")]
    [InlineData("invalid command")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NonSpecCommands_ReturnsNull(string command)
    {
        var result = CommandParser.Parse(command, "Player1", "123456");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("", "Player1", "123456", "Command text is empty")]
    [InlineData("move", "", "123456", "Challenger name is required")]
    [InlineData("move", "Player1", "", "OTP code is required")]
    [InlineData("hit main", "Player1", "123456", "Invalid command format: 'hit main'")]
    public void TryParseWithError_InvalidInputs_ReturnsError(
        string command, string name, string otp, string expectedError)
    {
        var result = CommandParser.TryParseWithError(command, name, otp);

        Assert.NotNull(result);
        Assert.Null(result.Value.Command);
        Assert.Equal(expectedError, result.Value.Error);
    }
}
