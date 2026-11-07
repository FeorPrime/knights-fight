using KnightFight.Game.Management;
using KnightFight.Game.Engine;
using Microsoft.Extensions.Logging;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// Drives the shared turn clock over every live match.
/// The turn rules live in the engine; this service only paces them.
/// </summary>
public class TurnService : ITurnService
{
    private readonly IGameManager _gameManager;
    private readonly ILogger<TurnService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    // readme: "each turn lasts 0.5 seconds"
    private const int _defaultTurnIntervalMs = 500;
    // Game-dev: guard against a config typo turning the clock into a busy loop.
    private const int _minTurnIntervalMs = 50;

    private int _turnIntervalMs;

    public TurnService(IGameManager gameManager, ILogger<TurnService> logger, IConfiguration configuration)
    {
        _gameManager = gameManager;
        _logger = logger;
        _turnIntervalMs = Math.Max(
            _minTurnIntervalMs,
            configuration.GetValue("TurnSettings:IntervalMs", _defaultTurnIntervalMs));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Game-dev: an async method with no await point runs eagerly on the caller, which
        // would hold up app startup forever. Task.Run puts the clock on its own worker.
        _runningTask = Task.Run(async () => await RunTurnsAsync(_cts.Token));
        _logger.LogInformation(
            "Turn service started, resolving a turn every {IntervalMs} ms",
            _turnIntervalMs);
        return Task.CompletedTask;
    }

    public async Task RunTurnsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ResolveTurns();
                Thread.Sleep(_turnIntervalMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Turn service loop stopped");
        }
    }

    private void ResolveTurns()
    {
        // The manager only ticks in-progress matches; finished ones keep their snapshot
        // so the match endpoints can still report the result.
        foreach (var (gameId, result) in _gameManager.TickAllActiveGames())
        {
            _logger.LogDebug(
                "Match {GameId} turn {TurnNumber}: actions {Action1}/{Action2}, events {Events}, status {Status}",
                gameId,
                result.TurnNumber,
                result.Fighter1Action,
                result.Fighter2Action,
                result.Events.Count,
                result.StateAfterTurn.Status);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        try
        {
            if (_runningTask != null)
            {
                await _runningTask;
            }
        }
        catch (Exception)
        {
            // Expected: the loop unwinds when the stop token is cancelled.
        }

        _logger.LogInformation("Turn service stopped");
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
