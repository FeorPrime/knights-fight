using System.Security.Claims;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.Services.Images;
using KnightFight.Api.Models.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KnightFight.Api.Features.Challenger.Avatar;

/// <summary>
/// Accepts a challenger's avatar image and stores it.
/// </summary>
/// <remarks>
/// This is the API's second authorized route. The JWT already carries the
/// challenger id in its "sub" claim, so the caller is identified without any
/// extra credential: a token for another challenger is rejected.
/// </remarks>
public class ChallengerAvatarUpload(
    IDbContextFactory<KfContext> factory,
    IImageService imageService,
    ILogger<ChallengerAvatarUpload> logger)
    : ApiMethod(ApiMap.Challenger.AVATAR)
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    private const string FileFieldName = "avatar";

    public override void MapEndpoint(WebApplication app)
    {
        // RequireAuthorization is explicit as well as the attribute, so the
        // requirement never depends on metadata inference.
        app.MapPost(UriPattern, Handler).RequireAuthorization().DisableAntiforgery();
    }

    [Authorize]
    [RequestSizeLimit(MaxUploadBytes)]
    [Tags("Challenger/Avatar")]
    [EndpointSummary("Uploads new avatar for specified challenger")]
    [EndpointDescription("Uploads new avatar for specified challenger")]
    public async Task<IResult> Handler(long id, HttpContext http, IFormFile? avatar)
    {
        var callerId = GetChallengerId(http.User);
        if (callerId is null)
            return Results.Unauthorized();

        if (callerId.Value != id)
            return Results.Forbid();

        if (avatar is null || avatar.Length == 0)
            return Results.BadRequest(new { message = $"A file part named '{FileFieldName}' is required." });

        if (avatar.Length > MaxUploadBytes)
            return Results.BadRequest(new { message = "The avatar is too large." });

        await using var upload = avatar.OpenReadStream();
        var normalized = await imageService.NormalizeAsync(upload, http.RequestAborted);

        if (!normalized.IsSuccess)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Avatar upload rejected: {Error}", normalized.Error);

            return Results.BadRequest(new { message = normalized.Error.ToString() });
        }

        await using var ctx = await factory.CreateDbContextAsync();

        var challenger = await ctx.Challengers.SingleOrDefaultAsync(c => c.Id == id);
        if (challenger is null)
            return Results.NotFound();

        challenger.AvatarBlob = normalized.Bytes;
        challenger.DefaultAvatarIndex = null;

        await ctx.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(new { id, avatarUrl = AvatarUrl.For(id) });
    }

    /// <summary>
    /// Reads the challenger id from the JWT. The "sub" claim is mapped to
    /// NameIdentifier by the default inbound claim mapping, so both are tried.
    /// </summary>
    private static long? GetChallengerId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(raw, out var id) ? id : null;
    }
}
