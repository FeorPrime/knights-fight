using System.Net;
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

    [Fact]
    public async Task ProcessAsync_ValidCommand_ReturnsSuccess()
    {
        var processor = new CommandProcessor(
            _otpServiceMock.Object,
            _otpSeedsProviderMock.Object,
            _loggerMock.Object
        );

        var command = new MovementCommand("Player1", "123456", MovementDirection.Up);

        _otpSeedsProviderMock.Setup(x => x.Get("Player1"))
            .ReturnsAsync(ExceptResult<string>.Success("SEED_123"));
        _otpServiceMock.Setup(x => x.CheckOtpTokenValidity("SEED_123", "123456"))
            .Returns(true);

        var result = await processor.ProcessAsync(command);

        Assert.True(result.Success);
        Assert.Contains("move up", result.Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidOtp_ReturnsFailure()
    {
        var processor = new CommandProcessor(
            _otpServiceMock.Object,
            _otpSeedsProviderMock.Object,
            _loggerMock.Object
        );

        var command = new MovementCommand("Player1", "WRONG_OTP", MovementDirection.Up);

        _otpSeedsProviderMock.Setup(x => x.Get("Player1"))
            .ReturnsAsync(ExceptResult<string>.Success("SEED_123"));
        _otpServiceMock.Setup(x => x.CheckOtpTokenValidity("SEED_123", "WRONG_OTP"))
            .Returns(false);

        var result = await processor.ProcessAsync(command);

        Assert.False(result.Success);
        Assert.Equal("Authentication failed", result.Error);
    }

    [Fact]
    public async Task ProcessAsync_UnknownChallenger_ReturnsFailure()
    {
        var processor = new CommandProcessor(
            _otpServiceMock.Object,
            _otpSeedsProviderMock.Object,
            _loggerMock.Object
        );

        var command = new MovementCommand("UnknownPlayer", "123456", MovementDirection.Up);

        _otpSeedsProviderMock.Setup(x => x.Get("UnknownPlayer"))
            .ReturnsAsync(ExceptResult<string>.Failure("Challenger not found"));

        var result = await processor.ProcessAsync(command);

        Assert.False(result.Success);
        Assert.Equal("Authentication failed", result.Error);
    }
}
