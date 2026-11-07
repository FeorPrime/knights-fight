using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Api.Models.Entities;

namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// DO NOT USE THIS IN PRODUCTION!!!
/// NEVER USE ANYTHING LIKE THIS OUTSIDE POC/TESTS/EDUCATION PROJECTS!!!
/// JUST GRAB AN OAUTH OR SIMILAR PROJECT!!!
/// NEVER TRY TO INVENT AUTH BY YOURSELF!!!
/// </summary>
public class AuthService : IAuthService
{
    internal static readonly string ISSUER = Assembly.GetExecutingAssembly().GetName().Name!;
    internal const string AUDIENCE = "KFClient";
    internal const int EXPIRATION_TIME_MINUTES = 10;

    public string GenerateToken(ChallengerModel user)
    {
        var secretKey = new SymmetricSecurityKey(CryptoService.STATIC_KEY_BYTES);
        
        var credentials = new SigningCredentials(
            secretKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.ChallengerName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, 
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), 
                ClaimValueTypes.Integer64)
        };

        // TODO#: Later need to add moderation, maybe?
        // if (roles != null)
        // {
        //     foreach (var role in roles)
        //         claims.Add(new Claim(ClaimTypes.Role, role));
        // }

        var token = new JwtSecurityToken(
            issuer: ISSUER,
            audience: AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(EXPIRATION_TIME_MINUTES),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
}