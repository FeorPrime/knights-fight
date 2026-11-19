using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Models.Mappers;
using KnightFight.Api.Models.Requests;
using KnightFight.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Challenger;

public class ChallengerGetAll(IDbContextFactory<KfContext> factory)
    : GetFilteredEndpoint<GetAllChallengerRequest>(ApiMap.Challenger.GET_ALL)
{
    private const int DEFAULT_PAGE = 0;
    private const int DEFAULT_PAGE_SIZE = 20;
    private const int MIN_PAGE_SIZE = 1;
    private const int MAX_PAGE_SIZE = 100;

    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, Handler);
    }

    [Tags("Challenger")]
    [EndpointSummary("Get all challengers")]
    [EndpointDescription("Show all players, who handle registration, filtered and sorted")]
    public override async Task<IResult> Handler([AsParameters] GetAllChallengerRequest request)
    {
        var page = Math.Max(request.Page ?? DEFAULT_PAGE, 0);
        var pageSize = Math.Clamp(request.PageSize ?? DEFAULT_PAGE_SIZE, MIN_PAGE_SIZE, MAX_PAGE_SIZE);

        await using var ctx = await factory.CreateDbContextAsync();

        var total = await ctx.Challengers.CountAsync();

        var items = await ctx.Challengers
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(c => new ChallengerItemResponse(c.Id, c.ChallengerName, c.CreatedAt, AvatarUrl.For(c.Id)))
            .ToListAsync();

        return Results.Ok(new PagedChallengersResponse(items, total, page, pageSize));
    }
}
