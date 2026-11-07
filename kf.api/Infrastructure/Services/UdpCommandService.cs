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
        _port = configuration.GetValue("UdpSettings:Port", 5050);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listeningTask = StartListeningAsync(_cts.Token);
        _logger.LogInformation("UDP command service starting on port {Port}", _port);
        return Task.CompletedTask;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        try
        {
            _udpClient = new UdpClient(_port);

            // Game-dev: Configure socket for better performance
            _udpClient.Client.ReceiveBufferSize = 65536;
            _udpClient.Client.SendBufferSize = 65536;
            _udpClient.Client.ReceiveTimeout = 5000;

            _logger.LogInformation("UDP server listening on port {Port}", _port);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP server failed to start");
            throw;
        }
        finally
        {
            _udpClient?.Close();
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
