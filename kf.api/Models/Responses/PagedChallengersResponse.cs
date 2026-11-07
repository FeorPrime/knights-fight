namespace KnightFight.Api.Models.Responses;

public record ChallengerItemResponse(
    long Id,
    string Name,
    DateTime RegisteredAt,
    string? AvatarUrl = null
);

public record PagedChallengersResponse(
    IReadOnlyList<ChallengerItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record ChallengerNewcomerEvent(
    long Id,
    string Name,
    DateTime RegisteredAt,
    string? AvatarUrl = null
);
