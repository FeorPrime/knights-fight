using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightFight.Api.Infrastructure.Models;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// UDP server for receiving game commands
/// Uses hosted service pattern for background processing
/// </summary>
public class UdpCommandService : IUdpCommandService
{
    private readonly ICommandProcessor _commandProcessor;
    private readonly ILogger<UdpCommandService> _logger;
    private readonly int _port;

    private UdpClient? _udpClient;
    private Task? _listeningTask;
    private CancellationTokenSource? _cts;

    // Statistics (game-dev: useful for monitoring)
    private long _packetsReceived = 0;
    private long _packetsProcessed = 0;
    private long _packetsInvalid = 0;

    public UdpCommandService(
        ICommandProcessor commandProcessor,
        ILogger<UdpCommandService> logger,
        IConfiguration configuration)
    {
        _commandProcessor = commandProcessor;
        _logger = logger;
        _port = configuration.GetValue("UdpSettings:Port", 5051);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.LogInformation("UDP command service starting on port {Port}", _port);

        // Game-dev: claim the port before the service reports itself as listening. A UDP port has
        // one global namespace, so a system service or a second API instance can already own it and
        // the bind is refused here. Letting that error escape fails the app — HTTP running with a
        // dead game-command channel is a quieter failure than a startup you can read.
        try
        {
            _udpClient = new UdpClient(_port);
        }
        catch (SocketException ex)
        {
            _logger.LogError(
                ex,
                "UDP server cannot claim port {Port} ({SocketError}, errno {ErrorCode}). Another listener " +
                "already owns it — find it with 'netstat -ano' and set another UdpSettings:Port.",
                _port, ex.SocketErrorCode, ex.ErrorCode);
            throw;
        }

        // Game-dev: Configure socket for better performance
        _udpClient.Client.ReceiveBufferSize = 65536;
        _udpClient.Client.SendBufferSize = 65536;
        _udpClient.Client.ReceiveTimeout = 5000;

        _logger.LogInformation("UDP server listening on port {Port}", _port);

        // Game-dev, unlike TurnService: this loop has await points, so the call returns a pending
        // promise here and the receive loop keeps running on its own.
        _listeningTask = StartListeningAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        var client = _udpClient ?? throw new InvalidOperationException("UDP listener is not bound");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken);
                _packetsReceived++;

                // Process in background to avoid blocking
                _ = Task.Run(() => ProcessPacketAsync(result.Buffer, result.RemoteEndPoint),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving UDP packet");
            }
        }
    }

    public async Task ProcessPacketAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            // Parse JSON packet
            var packetJson = Encoding.UTF8.GetString(data);
            var packet = JsonSerializer.Deserialize<UdpPacket>(packetJson);

            if (packet == null)
            {
                _packetsInvalid++;
                _logger.LogWarning("Received null/empty UDP packet from {EndPoint}", remoteEndPoint);
                return;
            }

            // Set remote endpoint for context
            packet.RemoteEndPoint = remoteEndPoint;

            // Validate packet structure
            if (!packet.Validate())
            {
                _packetsInvalid++;
                _logger.LogWarning("Invalid UDP packet from {EndPoint}: Packet validation failed",
                    remoteEndPoint);
                return;
            }

            // Parse to strongly-typed command
            var command = packet.ToCommand();
            if (command == null)
            {
                _packetsInvalid++;
                _logger.LogWarning("Failed to parse command from {EndPoint}: '{CommandText}'",
                    remoteEndPoint, packet.CommandText);
                return;
            }

            // Process the command
            var result = await _commandProcessor.ProcessAsync(command, remoteEndPoint);
            _packetsProcessed++;

            if (!result.Success)
            {
                _logger.LogDebug("Command processing failed: {Error}", result.Error);
            }
        }
        catch (JsonException ex)
        {
            _packetsInvalid++;
            _logger.LogWarning(ex, "Failed to parse JSON packet from {EndPoint}", remoteEndPoint);
        }
        catch (Exception ex)
        {
            _packetsInvalid++;
            _logger.LogError(ex, "Error processing UDP packet from {EndPoint}", remoteEndPoint);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        try
        {
            if (_listeningTask != null)
            {
                await _listeningTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Game-dev: the OS socket is released on stop; Dispose only releases the cached handle.
        _udpClient?.Close();

        _logger.LogInformation(
            "UDP command service stopped. Stats: Received={Received}, Processed={Processed}, Invalid={Invalid}",
            _packetsReceived, _packetsProcessed, _packetsInvalid);
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _udpClient?.Dispose();
    }
}
