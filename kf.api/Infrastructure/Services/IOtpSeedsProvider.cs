using KnightFight.Api.Infrastructure.Models;

namespace KnightFight.Api.Infrastructure.Services;

public record LobbyUserDto(
    string Name,
    bool IsReady,
    string Status,
    DateTime LastActive,
    DateTime? ReadySince
);

public interface IOtpSeedsProvider
{
    Task<ExceptResult<bool>> ForceAdd(string userId, string otpSeed);
    Task<ExceptResult<bool>> Remove(string userId);
    Task<ExceptResult<string>> Get(string userId);
    Task<ExceptResult<bool>> SetReady(string userId, bool isReady = true);
    Task<ExceptResult<bool>> Touch(string userId);
    Task<IReadOnlyList<LobbyUserDto>> GetLobbyUsers();
}