using System.Globalization;
using KnightFight.Api.Infrastructure;

namespace KnightFight.Api.Models.Mappers;

/// <summary>
/// Builds the URL a client uses to fetch a challenger's avatar.
/// </summary>
/// <remarks>
/// The URL is root-relative on purpose: in development the Vite proxy forwards
/// /api to the API, in production nginx does the same. A path-relative URL
/// would break on any SPA route other than "/".
/// </remarks>
public static class AvatarUrl
{
    public static string For(long challengerId)
        => "/" + ApiMap.Challenger.AVATAR.Replace(
            "{id}",
            challengerId.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
}
