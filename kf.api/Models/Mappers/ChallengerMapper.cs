using System.Linq.Expressions;
using KnightFight.Api.Models.Entities;
using KnightFight.Api.Models.Requests;
using KnightFight.Api.Models.Responses;

namespace KnightFight.Api.Models.Mappers;

public static class ChallengerMapper
{
    public static ChallengerModel ToEntity(this CreateChallengerRequest source, string secretHash, int defaultAvatarIndex)
        => new()
        {
            //Id = 0,
            // CreatedAt = default,
            // UpdatedAt = default,
            ChallengerName = source.ChallengerName,
            Secret = secretHash,
            DefaultAvatarIndex = defaultAvatarIndex,
            AvatarBlob = null,
            // Challenges = 0,
            // Wins = 0,
            // Loses = 0,
            // Draws = 0
        };

    /// <summary>
    /// Projection of a challenger entity onto its API response.
    /// Kept as an expression tree so EF Core translates it into SQL and selects
    /// only the exposed columns instead of materializing the whole entity.
    /// </summary>
    public static Expression<Func<ChallengerModel, GetChallengerResponse>> ToResponseProjection()
        => source => new GetChallengerResponse
        {
            ChallengerName = source.ChallengerName,
            Challenges = source.Challenges,
            Wins = source.Wins,
            Loses = source.Loses,
            Draws = source.Draws
        };
}