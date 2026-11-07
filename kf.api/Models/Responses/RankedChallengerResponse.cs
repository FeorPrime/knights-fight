namespace KnightFight.Api.Models.Responses;

public record RankedChallengerResponse(
    long Id,
    string Name,
    int Rank,
    int Wins,
    int Losses,
    int Score,
    string? AvatarUrl = null
);
