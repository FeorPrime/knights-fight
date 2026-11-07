using Microsoft.AspNetCore.Mvc;
using KnightFight.Api.Models.Requests.BaseRequests;

namespace KnightFight.Api.Models.Requests;

public record GetAllChallengerRequest : IPaginatedRequest
{
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    [FromQuery(Name = "page_size")]
    public int? PageSize { get; init; }

    [FromQuery(Name = "pageSize")]
    public int? PageSizeCamel { get; init; }

    public int? EffectivePageSize => PageSize ?? PageSizeCamel;
};