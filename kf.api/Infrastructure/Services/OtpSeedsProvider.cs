using System.Collections.Concurrent;
using KnightFight.Api.Infrastructure.Models;

namespace KnightFight.Api.Infrastructure.Services;

public class LoggedInUserSession
{
    public required string UserId { get; set; }
    public string? OtpSeed { get; set; }
    public bool IsReady { get; set; }

    /// <summary>
    /// When the challenger declared themselves ready. Null while they are not ready.
    /// </summary>
    public DateTime? ReadySince { get; set; }

    public DateTime LastActiveAt { get; set; }
}

public class OtpSeedsProvider : IOtpSeedsProvider
{
    private const string CHALLENGER_NOT_FOUND = "Challenger not found";
    private readonly TimeSpan _inactivityTimeout;
    private readonly TimeProvider _timeProvider;
    
    /// <summary>
    /// !WARNING! This implementation is not for multi instance usage.
    /// If you need more than 1 app instance, you should use a distributed cache.
    /// </summary>
    private readonly ConcurrentDictionary<string, LoggedInUserSession> _seeds = new();

    public OtpSeedsProvider() : this(TimeSpan.FromMinutes(1), TimeProvider.System) { }

    public OtpSeedsProvider(TimeSpan inactivityTimeout, TimeProvider? timeProvider = null)
    {
        _inactivityTimeout = inactivityTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
    
    public Task<ExceptResult<bool>> ForceAdd(string userId, string otpSeed)
    {
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var session = new LoggedInUserSession
            {
                UserId = userId,
                OtpSeed = otpSeed,
                IsReady = false,
                LastActiveAt = now
            };
            _ = _seeds.AddOrUpdate(userId, session, (_, _) => session);
            return Task.FromResult(ExceptResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExceptResult<bool>.Failure(ex.Message));
        }
    }

    public Task<ExceptResult<bool>> Remove(string userId)
    {
        if (userId == null)
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        return Task.FromResult(_seeds.TryRemove(userId, out _) 
            ? ExceptResult<bool>.Success(true) 
            : ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
    }

    public Task<ExceptResult<string>> Get(string userId)
    {
        if (userId == null)
        {
            return Task.FromResult(ExceptResult<string>.Failure(CHALLENGER_NOT_FOUND));
        }

        if (!_seeds.TryGetValue(userId, out var session))
        {
            return Task.FromResult(ExceptResult<string>.Failure(CHALLENGER_NOT_FOUND));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now - session.LastActiveAt > _inactivityTimeout)
        {
            _seeds.TryRemove(userId, out _);
            return Task.FromResult(ExceptResult<string>.Failure(CHALLENGER_NOT_FOUND));
        }

        session.LastActiveAt = now;
        return Task.FromResult(ExceptResult<string>.Success(session.OtpSeed!));
    }

    public Task<ExceptResult<bool>> SetReady(string userId, bool isReady = true)
    {
        if (userId == null)
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        if (!_seeds.TryGetValue(userId, out var session))
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now - session.LastActiveAt > _inactivityTimeout)
        {
            _seeds.TryRemove(userId, out _);
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        var wasReady = session.IsReady;
        session.IsReady = isReady;
        session.ReadySince = isReady
            ? (wasReady ? session.ReadySince ?? now : now)
            : null;
        session.LastActiveAt = now;
        return Task.FromResult(ExceptResult<bool>.Success(true));
    }

    public Task<ExceptResult<bool>> Touch(string userId)
    {
        if (userId == null)
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        if (!_seeds.TryGetValue(userId, out var session))
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now - session.LastActiveAt > _inactivityTimeout)
        {
            _seeds.TryRemove(userId, out _);
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        session.LastActiveAt = now;
        return Task.FromResult(ExceptResult<bool>.Success(true));
    }

    public Task<IReadOnlyList<LobbyUserDto>> GetLobbyUsers()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = new List<LobbyUserDto>();

        foreach (var kvp in _seeds)
        {
            var session = kvp.Value;
            if (now - session.LastActiveAt > _inactivityTimeout)
            {
                _seeds.TryRemove(kvp.Key, out _);
            }
            else
            {
                result.Add(new LobbyUserDto(
                    session.UserId,
                    session.IsReady,
                    session.IsReady ? "ready" : "unready",
                    session.LastActiveAt,
                    session.ReadySince
                ));
            }
        }

        return Task.FromResult<IReadOnlyList<LobbyUserDto>>(result);
    }
}