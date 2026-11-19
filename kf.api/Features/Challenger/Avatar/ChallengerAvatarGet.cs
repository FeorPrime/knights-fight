using System.Security.Cryptography;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Challenger.Avatar;

/// <summary>
/// Serves a challenger's avatar as PNG.
/// </summary>
/// <remarks>
/// The route is anonymous: the avatar is public data and the leaderboard
/// renders it without a login. A challenger that was given a custom avatar
/// serves the stored blob; one that was not serves the bundled default its
/// row points at.
/// </remarks>
public class ChallengerAvatarGet(
    IDbContextFactory<KfContext> factory,
    IDefaultAvatarProvider defaultAvatars)
    : ApiMethod(ApiMap.Challenger.AVATAR)
{
    private const string CacheControl = "public, max-age=300";

    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, Handler);
    }
    
    [Tags("Challenger/Avatar")]
    [EndpointSummary("Gets challenger avatar")]
    [EndpointDescription("Gets challenger avatar")]
    public async Task<IResult> Handler(long id, HttpContext http)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var challenger = await ctx.Challengers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id);

        if (challenger is null)
            return Results.NotFound();

        var bytes = ResolveBytes(challenger);
        if (bytes is null)
            return Results.NotFound();

        var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes))[..16]}\"";

        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = CacheControl;

        var ifNoneMatch = http.Request.Headers.IfNoneMatch;
        if (ifNoneMatch.Count > 0 &&
            ifNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.File(bytes, "image/png");
    }

    private byte[]? ResolveBytes(ChallengerModel challenger)
    {
        if (challenger.AvatarBlob is not null)
            return challenger.AvatarBlob;

        if (challenger.DefaultAvatarIndex is not int index)
            return null;

        // an index outside the bundled set means the asset was removed - serve nothing
        if (index < 0 || index >= defaultAvatars.Avatars.Count)
            return null;

        return defaultAvatars.GetBytes(index);
    }
}
