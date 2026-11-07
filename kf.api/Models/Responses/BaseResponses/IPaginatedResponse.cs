namespace KnightFight.Api.Models.Responses.BaseResponses;

public interface IPaginatedResponse
{
    int? Page { get; init; }
    int? PageSize { get; init; }
    int? Pages { get; init; }
}