using System.Net;
using KnightFight.Game.Management;
using KnightFight.Game.Actions;
using KnightFight.Game.Entities;
using KnightFight.Api.Infrastructure.Models;
using KnightFight.Api.Infrastructure.Models.Commands;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using Microsoft.Extensions.Logging;
using Moq;

namespace kf.api.Tests.Infrastructure.Services;

public class CommandProcessorTests
{
    private readonly Mock<IOtpService> _otpServiceMock = new();
    private readonly Mock<IOtpSeedsProvider> _otpSeedsProviderMock = new();
    private readonly Mock<ILogger<CommandProcessor>> _loggerMock = new();

    private CommandProcessor CreateProcessor(GameManager gameManager) =>
        new CommandProcessor(
            _otpServiceMock.Object,
            _otpSeedsProviderMock.Object,
            gameManager,
            _loggerMock.Object
        );

    private void AcceptOtp(string challengerName, string otpCode, bool valid)
    {
        _otpSeedsProviderMock.Setup(x => x.Get(challengerName))
            .ReturnsAsync(ExceptResult<string>.Success("SEED_123"));
        _otpServiceMock.Setup(x => x.CheckOtpTokenValidity("SEED_123", otpCode))
            .Returns(valid);
    }

    [Fact]
    public async Task ProcessAsync_QueueActionIntoLiveMatch()
    {
        var gameManager = new GameManager();
        var game = gameManager.CreateGame("Player1", "Rival");
        AcceptOtp("Player1", "123456", true);

        var processor = CreateProcessor(gameManager);
        var result = await processor.ProcessAsync(new MovementCommand("Player1", "123456"));

        Assert.True(result.Success);
        Assert.Equal("Command 'move' queued", result.Message);

        // readme: the packet carries only the challenger name, and that is the engine's
        // fighter id — the match must be resolvable from it.
        Assert.Equal(game, gameManager.GetGameByChallenger("PLAYER1"));
        Assert.Equal(1, game.Fighter1.BufferedMovesCount);
        Assert.Equal(GameAction.Move, game.GetBufferedActions("Player1")[0]);
    }

    [Fact]
    public async Task ProcessAsync_RejectsFourthBufferedAction()
    {
        var gameManager = new GameManager();
        var game = gameManager.CreateGame("Player1", "Rival");
        AcceptOtp("Player1", "123456", true);

        // readme: "only 3 received packets enter the queue, the rest are discarded."
        for (var _ = 0; _ < Fighter.MaxBufferedMoves; _++)
        {
            Assert.True(gameManager.EnqueueAction(game.Id, "Player1", GameAction.Move));
        }

        var processor = CreateProcessor(gameManager);
        var result = await processor.ProcessAsync(new StrikeCommand("Player1", "123456", StrikeSide.Main));

        Assert.False(result.Success);
        Assert.Equal("Action rejected: queue full or match finished", result.Error);
        Assert.Equal(Fighter.MaxBufferedMoves, game.Fighter1.BufferedMovesCount);
    }

    [Fact]
    public async Task ProcessAsync_AuthenticatedChallengerWithoutLiveMatch()
    {
        var gameManager = new GameManager();
        AcceptOtp("Player1", "123456", true);

        var processor = CreateProcessor(gameManager);
        var result = await processor.ProcessAsync(new TurnCommand(
            "Player1", "123456", TurnDirection.Left));

        Assert.False(result.Success);
        Assert.Equal("No live match for challenger", result.Error);
        Assert.Null(gameManager.GetGameByChallenger("Player1"));
    }

    [Fact]
    public async Task ProcessAsync_InvalidOtp_ReturnsFailure()
    {
        var gameManager = new GameManager();
        _ = gameManager.CreateGame("Player1", "Rival");
        AcceptOtp("Player1", "WRONG_OTP", false);

        var processor = CreateProcessor(gameManager);
        var result = await processor.ProcessAsync(new MovementCommand("Player1", "WRONG_OTP"));

        Assert.False(result.Success);
        Assert.Equal("Authentication failed", result.Error);
    }

    [Fact]
    public async Task ProcessAsync_UnknownChallenger_ReturnsFailure()
    {
        var gameManager = new GameManager();
        _otpSeedsProviderMock.Setup(x => x.Get("UnknownPlayer"))
            .ReturnsAsync(ExceptResult<string>.Failure("Challenger not found"));

        var processor = CreateProcessor(gameManager);
        var result = await processor.ProcessAsync(
            new MovementCommand("UnknownPlayer", "123456"));

        Assert.False(result.Success);
        Assert.Equal("Authentication failed", result.Error);
    }
}
