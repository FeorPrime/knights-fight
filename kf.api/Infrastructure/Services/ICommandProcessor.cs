using System.Net;
using KnightFight.Api.Infrastructure.Models.Commands;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// Handles game command processing (separate from UDP transport)
/// </summary>
public interface ICommandProcessor
{
    /// <summary>
    /// Process a validated command
    /// </summary>
    Task<CommandResult> ProcessAsync(IGameCommand command, IPEndPoint? remoteEndPoint = null);

    /// <summary>
    /// Validate challenger and OTP
    /// </summary>
    Task<bool> ValidateAuthenticationAsync(string challengerName, string otpCode);
}

/// <summary>
/// Result of command processing
/// </summary>
public record CommandResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Error { get; init; }

    public static CommandResult SuccessResult(string message = "") => new() { Success = true, Message = message };
    public static CommandResult FailureResult(string error) => new() { Success = false, Error = error };
}
