namespace KnightFight.Api.Models.Requests.BaseRequests;

interface IPaginatedRequest
{
    int? Page { get; init; }
    int? PageSize { get; init; }
} 