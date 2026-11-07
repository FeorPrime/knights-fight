using System.Collections.Concurrent;
using System.Net;
using KnightFight.Api.Infrastructure.Models.Commands;
using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Game.Management;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// Processes game commands with authentication and validation
/// </summary>
public class CommandProcessor : ICommandProcessor
{
    private readonly IOtpService _otpService;
    private readonly IOtpSeedsProvider _otpSeedsProvider;
    private readonly IGameManager _gameManager;
    private readonly ILogger<CommandProcessor> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _rateLimits = new();

    // Game-dev: Rate limiting to prevent command flooding
    private readonly TimeSpan _rateLimitWindow = TimeSpan.FromMilliseconds(100);
    private readonly int _maxCommandsPerWindow = 10;

    public CommandProcessor(
        IOtpService otpService,
        IOtpSeedsProvider otpSeedsProvider,
        IGameManager gameManager,
        ILogger<CommandProcessor> logger)
    {
        _otpService = otpService;
        _otpSeedsProvider = otpSeedsProvider;
        _gameManager = gameManager;
        _logger = logger;
    }

    public async Task<CommandResult> ProcessAsync(IGameCommand command, IPEndPoint? remoteEndPoint = null)
    {
        // Rate limiting check
        if (!CheckRateLimit(command.ChallengerName))
        {
            _logger.LogWarning("Rate limit exceeded for {ChallengerName}", command.ChallengerName);
            return CommandResult.FailureResult("Rate limit exceeded");
        }

        // Authentication
        var isAuthenticated = await ValidateAuthenticationAsync(command.ChallengerName, command.OtpCode);
        if (!isAuthenticated)
        {
            _logger.LogWarning("Authentication failed for {ChallengerName}", command.ChallengerName);
            return CommandResult.FailureResult("Authentication failed");
        }

        // Log valid command (game-dev: use structured logging)
        _logger.LogInformation(
            "Command: {CommandType} - Challenger: {ChallengerName} - Command: {Command} - From: {EndPoint}",
            command.Category,
            command.ChallengerName,
            command.ToStringCommand(),
            remoteEndPoint?.ToString() ?? "unknown"
        );

        // Console output as requested
        Console.WriteLine("[COMMAND] {0}: {1}", command.ChallengerName, command.ToStringCommand());

        // readme: "The match must already be started" — the challenger name is the engine's
        // fighter id, so it is the only handle a UDP packet carries.
        var game = _gameManager.GetGameByChallenger(command.ChallengerName);
        if (game is null)
        {
            _logger.LogDebug("No live match for challenger {ChallengerName}", command.ChallengerName);
            return CommandResult.FailureResult("No live match for challenger");
        }

        // readme: only 3 received packets enter the queue, the rest are discarded.
        if (!_gameManager.EnqueueAction(game.Id, command.ChallengerName, command.ToGameAction()))
        {
            _logger.LogDebug(
                "Action rejected for challenger {ChallengerName} in match {GameId}",
                command.ChallengerName,
                game.Id);
            return CommandResult.FailureResult("Action rejected: queue full or match finished");
        }

        return CommandResult.SuccessResult(string.Format("Command '{0}' queued", command.ToStringCommand()));
    }

    public async Task<bool> ValidateAuthenticationAsync(string challengerName, string otpCode)
    {
        var seedResult = await _otpSeedsProvider.Get(challengerName);
        if (!seedResult.IsSuccess || seedResult.ResultValue == null)
        {
            _logger.LogDebug("Challenger {ChallengerName} not found", challengerName);
            return false;
        }

        var isValid = _otpService.CheckOtpTokenValidity(seedResult.ResultValue, otpCode);
        if (!isValid)
        {
            _logger.LogDebug("Invalid OTP for challenger {ChallengerName}", challengerName);
        }

        return isValid;
    }

    private bool CheckRateLimit(string challengerName)
    {
        var now = DateTime.UtcNow;
        if (_rateLimits.TryGetValue(challengerName, out var lastCommandTime))
        {
            if (now - lastCommandTime < _rateLimitWindow)
            {
                return false;
            }
        }

        _rateLimits.AddOrUpdate(challengerName, now, (_, _) => now);
        return true;
    }
}
