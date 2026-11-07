using System.Net;
using System.Text.Json.Serialization;
using KnightFight.Api.Infrastructure.Models.Commands;

namespace KnightFight.Api.Infrastructure.Models;

/// <summary>
/// Represents a UDP packet with protocol versioning
/// </summary>
public class UdpPacket
{
    /// <summary>
    /// Current protocol version (increment when changing packet structure)
    /// </summary>
    [JsonPropertyName("v")]
    public byte ProtocolVersion { get; set; } = 1;

    /// <summary>
    /// Sequence number to detect packet loss
    /// </summary>
    [JsonPropertyName("seq")]
    public uint SequenceNumber { get; set; }

    /// <summary>
    /// Unix timestamp in milliseconds (for latency compensation)
    /// </summary>
    [JsonPropertyName("ts")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Challenger name
    /// </summary>
    [JsonPropertyName("n")]
    public string ChallengerName { get; set; } = string.Empty;

    /// <summary>
    /// OTP code
    /// </summary>
    [JsonPropertyName("otp")]
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>
    /// Command as string (for JSON) or command ID (for binary)
    /// </summary>
    [JsonPropertyName("cmd")]
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// Optional checksum for data integrity
    /// </summary>
    [JsonIgnore]
    public uint Checksum { get; set; }

    public IPEndPoint? RemoteEndPoint { get; set; }

    /// <summary>
    /// Parse into strongly-typed command
    /// </summary>
    public IGameCommand? ToCommand() =>
        CommandParser.Parse(CommandText, ChallengerName, OtpCode);

    /// <summary>
    /// Validate the entire packet
    /// </summary>
    public bool Validate()
    {
        if (ProtocolVersion != 1)
            return false;

        if (string.IsNullOrWhiteSpace(ChallengerName))
            return false;

        if (string.IsNullOrWhiteSpace(OtpCode))
            return false;

        if (string.IsNullOrWhiteSpace(CommandText))
            return false;

        var command = ToCommand();
        return command?.Validate() ?? false;
    }
}
