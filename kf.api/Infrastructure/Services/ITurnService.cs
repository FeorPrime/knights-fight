using KnightFight.Game.Management;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// Drives the shared turn clock: every turn resolves one queued packet per fighter.
/// The engine owns the turn rules (kf.game); this service only paces them.
/// </summary>
public interface ITurnService : IHostedService, IDisposable
{
    Task RunTurnsAsync(CancellationToken cancellationToken);
}
