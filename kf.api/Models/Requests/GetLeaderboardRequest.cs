using Microsoft.AspNetCore.Mvc;

namespace KnightFight.Api.Models.Requests;

public record GetLeaderboardRequest
{
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }
}
