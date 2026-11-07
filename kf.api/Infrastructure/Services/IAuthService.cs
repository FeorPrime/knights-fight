using KnightFight.Api.Models.Entities;

namespace KnightFight.Api.Infrastructure.Services;

public interface IAuthService
{
    string GenerateToken(ChallengerModel user);
}