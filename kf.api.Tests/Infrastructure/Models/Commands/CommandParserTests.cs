using KnightFight.Api.Infrastructure.Models.Commands;

namespace kf.api.Tests.Infrastructure.Models.Commands;

public class CommandParserTests
{
    [Theory]
    [InlineData("move up", "Player1", "123456", typeof(MovementCommand))]
    [InlineData("MOVE DOWN", "Player1", "123456", typeof(MovementCommand))]
    [InlineData("move left", "Player1", "123456", typeof(MovementCommand))]
    [InlineData("move right", "Player1", "123456", typeof(MovementCommand))]
    [InlineData("use hand_left", "Player1", "123456", typeof(HandCommand))]
    [InlineData("USE HAND_RIGHT", "Player1", "123456", typeof(HandCommand))]
    [InlineData("turn left", "Player1", "123456", typeof(TurnCommand))]
    [InlineData("TURN RIGHT", "Player1", "123456", typeof(TurnCommand))]
    public void Parse_ValidCommands_ReturnsCorrectType(string command, string name, string otp, Type expectedType)
    {
        var result = CommandParser.Parse(command, name, otp);
        Assert.NotNull(result);
        Assert.IsType(expectedType, result);
        Assert.True(result.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid command")]
    [InlineData("move diagonally")]
    [InlineData("use hand_middle")]
    [InlineData("turn around")]
    public void Parse_InvalidCommands_ReturnsNull(string command)
    {
        var result = CommandParser.Parse(command, "Player1", "123456");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("", "Player1", "123456", "Command text is empty")]
    [InlineData("move up", "", "123456", "Challenger name is required")]
    [InlineData("move up", "Player1", "", "OTP code is required")]
    public void TryParseWithError_InvalidInputs_ReturnsError(
        string command, string name, string otp, string expectedError)
    {
        var result = CommandParser.TryParseWithError(command, name, otp);
        Assert.NotNull(result);
        Assert.Null(result.Value.Command);
        Assert.Equal(expectedError, result.Value.Error);
    }
}
