using System.Net;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// UDP service for receiving game commands
/// </summary>
public interface IUdpCommandService : IHostedService, IDisposable
{
    Task StartListeningAsync(CancellationToken cancellationToken);
    Task ProcessPacketAsync(byte[] data, IPEndPoint remoteEndPoint);
}
