namespace KnightFight.Api.Models.Responses;

public record MatchDto(
    string Id,
    string Challenger1,
    string Challenger2,
    string Status,
    DateTime StartedAt
);
