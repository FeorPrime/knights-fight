using KnightFight.Api.Models.Responses.BaseResponses;

namespace KnightFight.Api.Models.Responses;

public record GetAllChallengerResponse: IPaginatedResponse
{
    public IEnumerable<GetChallengerResponse> Challengers { get; init; } = Array.Empty<GetChallengerResponse>();

    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public int? Pages { get; init; }
}