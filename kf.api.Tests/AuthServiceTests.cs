using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Api.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KnightFight.Api.Tests;

/// <summary>
/// Behaviour tests for <see cref="AuthService"/> - the JWT issuer of the API.
/// <para>
/// The service is used by the login endpoint (<c>AuthGetTokens</c>) and the tokens it issues are
/// later consumed by the JWT bearer middleware configured in <c>Program.cs</c>. That makes the
/// produced token a contract between two places, so these tests pin down the whole contract:
/// the wire format, the claims, the registered claims (iss/aud/exp), the signature and the
/// behaviour of the validation the API performs on incoming requests.
/// </para>
/// <para>
/// The validation part deliberately reuses the exact <see cref="TokenValidationParameters"/>
/// that <c>Program.cs</c> builds, so a drift between the issuer and the validator fails here
/// instead of in production.
/// </para>
/// </summary>
public class AuthServiceTests
{
    /// <summary>
    /// Claims that the JWT specification owns. Everything else in the payload is a claim the
    /// service itself adds.
    /// </summary>
    private static readonly string[] RegisteredClaimNames = { "iss", "aud", "exp", "nbf" };

    /// <summary>
    /// The lifetime of an issued token in seconds - ten minutes. Kept as a literal so the
    /// expectations do not depend on the constant under test.
    /// </summary>
    private const int ExpectedLifetimeSeconds = 10 * 60;

    private readonly IAuthService _authService = new AuthService();

    #region helpers

    private static ChallengerModel CreateChallenger(
        long id = 4242,
        string name = "sir-knight",
        string secret = "otp-seed-secret")
        => new()
        {
            Id = id,
            ChallengerName = name,
            Secret = secret
        };

    /// <summary>
    /// Decodes a base64url encoded JWT segment (the alphabet of the JWT spec uses '-' and '_'
    /// instead of '+' and '/' and drops the padding).
    /// </summary>
    private static string DecodeSegment(string segment)
    {
        string padded = segment.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => ""
        };

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    /// <summary>
    /// Encodes a string as a base64url segment - the counterpart of
    /// <see cref="DecodeSegment"/>, used to rebuild a tampered payload.
    /// </summary>
    private static string EncodeSegment(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static JsonElement ReadHeader(string token)
    {
        using JsonDocument document = JsonDocument.Parse(DecodeSegment(token.Split('.')[0]));
        return document.RootElement.Clone();
    }

    private static JsonElement ReadPayload(string token)
    {
        using JsonDocument document = JsonDocument.Parse(DecodeSegment(token.Split('.')[1]));
        return document.RootElement.Clone();
    }

    private static string[] CustomClaimNames(JsonElement payload)
        => payload.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => !RegisteredClaimNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static long ReadUnixTimestamp(JsonElement payload, string name)
    {
        JsonElement value = payload.GetProperty(name);
        return value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : long.Parse(value.GetString()!);
    }

    /// <summary>
    /// The very same validation setup the API uses for incoming requests - see <c>Program.cs</c>.
    /// </summary>
    private static TokenValidationParameters BuildApiValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = AuthService.ISSUER,
        ValidAudience = AuthService.AUDIENCE,
        IssuerSigningKey = new SymmetricSecurityKey(CryptoService.STATIC_KEY_BYTES),
        ClockSkew = TimeSpan.Zero
    };

    /// <summary>
    /// A handler that keeps the original claim types (<c>sub</c>, <c>unique_name</c>, ...) instead
    /// of translating them to the long WS-Federation URIs.
    /// </summary>
    private static JwtSecurityTokenHandler CreateHandler() => new() { MapInboundClaims = false };

    /// <summary>
    /// Signs an arbitrary token - used to build the "attacker" tokens the validation has to
    /// reject.
    /// </summary>
    private static string SignToken(
        IEnumerable<Claim> claims,
        byte[] keyBytes,
        string issuer,
        string audience,
        DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Claim[] ForeignTokenClaims() =>
    [
        new Claim(JwtRegisteredClaimNames.Sub, "4242"),
        new Claim(JwtRegisteredClaimNames.UniqueName, "sir-knight")
    ];

    #endregion

    #region format

    [Fact]
    public void GenerateToken_ReturnsANonEmptyString()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_ProducesThreeDotSeparatedSegments()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        string[] segments = token.Split('.');

        Assert.Equal(3, segments.Length);
        Assert.All(segments, segment => Assert.False(string.IsNullOrEmpty(segment)));
    }

    [Fact]
    public void GenerateToken_HeaderDeclaresTheHs256Algorithm()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        JsonElement header = ReadHeader(token);

        Assert.Equal("HS256", header.GetProperty("alg").GetString());
    }

    [Fact]
    public void GenerateToken_HeaderDeclaresTheJwtType()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        JsonElement header = ReadHeader(token);

        Assert.Equal("JWT", header.GetProperty("typ").GetString());
    }

    [Fact]
    public void GenerateToken_PayloadCarriesExactlyTheFourExpectedClaims()
    {
        // The service adds sub, unique_name, jti and iat. The role/moderation claim block is
        // still a TODO in the production code, so today nothing else may show up in the payload.
        string token = _authService.GenerateToken(CreateChallenger());

        JsonElement payload = ReadPayload(token);

        Assert.Equal(new[] { "iat", "jti", "sub", "unique_name" }, CustomClaimNames(payload));
    }

    #endregion

    #region claims

    [Fact]
    public void GenerateToken_SubjectClaimCarriesTheUserId()
    {
        var challenger = CreateChallenger(id: 4242);

        string token = _authService.GenerateToken(challenger);

        Assert.Equal("4242", ReadPayload(token).GetProperty("sub").GetString());
    }

    [Fact]
    public void GenerateToken_UniqueNameClaimCarriesTheChallengerName()
    {
        var challenger = CreateChallenger(name: "sir-knight");

        string token = _authService.GenerateToken(challenger);

        Assert.Equal("sir-knight", ReadPayload(token).GetProperty("unique_name").GetString());
    }

    [Fact]
    public void GenerateToken_JtiClaimIsAGuid()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        string jti = ReadPayload(token).GetProperty("jti").GetString()!;

        Assert.True(Guid.TryParse(jti, out _), $"The jti claim '{jti}' is not a Guid.");
    }

    [Fact]
    public void GenerateToken_JtiDiffersBetweenTwoCalls()
    {
        var challenger = CreateChallenger();

        string first = ReadPayload(_authService.GenerateToken(challenger)).GetProperty("jti").GetString()!;
        string second = ReadPayload(_authService.GenerateToken(challenger)).GetProperty("jti").GetString()!;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateToken_IatClaimIsTheCurrentUnixTimestamp()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string token = _authService.GenerateToken(CreateChallenger());

        long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        long iat = ReadUnixTimestamp(ReadPayload(token), "iat");

        Assert.InRange(iat, before, after);
    }

    [Fact]
    public void GenerateToken_NeverLeaksTheOtpSecret()
    {
        // The secret is the shared TOTP seed - it authenticates the user on its own, so it must
        // never end up inside a token that travels to the client.
        var challenger = CreateChallenger(secret: "TOP-SECRET-OTP-SEED-VALUE");

        string token = _authService.GenerateToken(challenger);

        JsonElement payload = ReadPayload(token);

        Assert.False(payload.TryGetProperty("secret", out _));
        Assert.DoesNotContain(challenger.Secret, payload.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(challenger.Secret, token, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateToken_DoesNotLeakTheWinLossStatistics()
    {
        var challenger = CreateChallenger();
        challenger.Wins = 42;
        challenger.Loses = 7;
        challenger.Draws = 3;
        challenger.Challenges = 52;

        string token = _authService.GenerateToken(challenger);

        JsonElement payload = ReadPayload(token);

        Assert.DoesNotContain(
            payload.EnumerateObject(),
            property => property.Name is "wins" or "loses" or "draws" or "challenges");
    }

    #endregion

    #region registered claims

    [Fact]
    public void GenerateToken_IsserIsTheApiAssemblyName()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("kf.api", parsed.Issuer);
        Assert.Equal(typeof(AuthService).Assembly.GetName().Name, parsed.Issuer);
    }

    [Fact]
    public void GenerateToken_AudienceIsKfClient()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(new[] { "KFClient" }, parsed.Audiences);
        Assert.Equal("KFClient", AuthService.AUDIENCE);
    }

    [Fact]
    public void GenerateToken_ExpiresTenMinutesAfterItWasIssued()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        JsonElement payload = ReadPayload(token);
        long iat = ReadUnixTimestamp(payload, "iat");
        long exp = ReadUnixTimestamp(payload, "exp");

        // The ten minutes are written as a literal on purpose: reading the expected value back
        // from AuthService.EXPIRATION_TIME_MINUTES would make the test pass for any lifetime.
        // One second of slack in both directions: the iat and the exp are computed from two
        // separate DateTime.UtcNow calls, so a second boundary may fall in between.
        Assert.InRange(exp - iat, ExpectedLifetimeSeconds - 1, ExpectedLifetimeSeconds + 1);
    }

    [Fact]
    public void GenerateToken_ExpirationTimeIsTenMinutes()
    {
        // The lifetime is part of the contract the clients rely on, so it is pinned here as a
        // literal instead of being read back from the constant under test.
        Assert.Equal(10, AuthService.EXPIRATION_TIME_MINUTES);
    }

    [Fact]
    public void GenerateToken_ExpirationIsInTheFuture()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        long exp = ReadUnixTimestamp(ReadPayload(token), "exp");
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Assert.InRange(exp, now + ExpectedLifetimeSeconds - 5, now + ExpectedLifetimeSeconds + 5);
    }

    #endregion

    #region signature and validation

    [Fact]
    public void GenerateToken_PassesValidationWithTheApiValidationParameters()
    {
        var challenger = CreateChallenger(id: 4242, name: "sir-knight");

        string token = _authService.GenerateToken(challenger);

        var handler = CreateHandler();
        ClaimsPrincipal principal = handler.ValidateToken(token, BuildApiValidationParameters(), out _);

        Assert.Equal("4242", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal("sir-knight", principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value);
    }

    [Fact]
    public void GenerateToken_FailsValidationWhenTheSignatureIsTamperedWith()
    {
        string token = _authService.GenerateToken(CreateChallenger());

        string[] parts = token.Split('.');
        char[] signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'a' ? 'b' : 'a';
        string tampered = $"{parts[0]}.{parts[1]}.{new string(signature)}";

        var handler = CreateHandler();

        // A forged token must be rejected. The concrete exception depends on how the handler
        // resolves the signing key: without a "kid" header the failure surfaces as
        // SecurityTokenSignatureKeyNotFoundException, otherwise as
        // SecurityTokenInvalidSignatureException - both mean "not accepted".
        Assert.ThrowsAny<SecurityTokenException>(
            () => handler.ValidateToken(tampered, BuildApiValidationParameters(), out _));
    }

    [Fact]
    public void GenerateToken_FailsValidationWhenThePayloadIsTamperedWith()
    {
        // The subject is rewritten while the original signature is kept: a token like this is
        // exactly what an attacker would send to impersonate another player.
        string token = _authService.GenerateToken(CreateChallenger(id: 1));

        string[] parts = token.Split('.');
        JsonObject payload = JsonNode.Parse(DecodeSegment(parts[1]))!.AsObject();
        payload["sub"] = "999";
        string tamperedPayload = EncodeSegment(payload.ToJsonString());
        string tampered = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

        var handler = CreateHandler();

        Assert.ThrowsAny<SecurityTokenException>(
            () => handler.ValidateToken(tampered, BuildApiValidationParameters(), out _));
    }

    [Fact]
    public void GenerateToken_FailsValidationWhenTheTokenWasSignedWithAnotherKey()
    {
        string foreignToken = SignToken(
            ForeignTokenClaims(),
            RandomNumberGenerator.GetBytes(64),
            AuthService.ISSUER,
            AuthService.AUDIENCE,
            DateTime.UtcNow.AddSeconds(ExpectedLifetimeSeconds));

        var handler = CreateHandler();

        Assert.ThrowsAny<SecurityTokenException>(
            () => handler.ValidateToken(foreignToken, BuildApiValidationParameters(), out _));
    }

    [Fact]
    public void ValidationHarness_RejectsATokenIssuedForAnotherAudience()
    {
        // Control test: proves the validation above is really strict about the audience.
        string foreignToken = SignToken(
            ForeignTokenClaims(),
            CryptoService.STATIC_KEY_BYTES,
            AuthService.ISSUER,
            "SomeOtherClient",
            DateTime.UtcNow.AddSeconds(ExpectedLifetimeSeconds));

        var handler = CreateHandler();

        Assert.Throws<SecurityTokenInvalidAudienceException>(
            () => handler.ValidateToken(foreignToken, BuildApiValidationParameters(), out _));
    }

    [Fact]
    public void ValidationHarness_RejectsATokenIssuedByAnotherIssuer()
    {
        // Control test: proves the validation above is really strict about the issuer.
        string foreignToken = SignToken(
            ForeignTokenClaims(),
            CryptoService.STATIC_KEY_BYTES,
            "SomeOtherIssuer",
            AuthService.AUDIENCE,
            DateTime.UtcNow.AddSeconds(ExpectedLifetimeSeconds));

        var handler = CreateHandler();

        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => handler.ValidateToken(foreignToken, BuildApiValidationParameters(), out _));
    }

    [Fact]
    public void ValidationHarness_RejectsAnExpiredToken()
    {
        // Control test: proves the lifetime check is active, so the ten minute window above
        // really ends the validity of a token.
        string expiredToken = SignToken(
            ForeignTokenClaims(),
            CryptoService.STATIC_KEY_BYTES,
            AuthService.ISSUER,
            AuthService.AUDIENCE,
            DateTime.UtcNow.AddMinutes(-5));

        var handler = CreateHandler();

        Assert.Throws<SecurityTokenExpiredException>(
            () => handler.ValidateToken(expiredToken, BuildApiValidationParameters(), out _));
    }

    [Fact]
    public void SigningKey_UsedByAuthServiceMatchesTheKeyProgramCsValidatesWith()
    {
        // AuthService signs with CryptoService.STATIC_KEY_BYTES while Program.cs validates with
        // Encoding.UTF8.GetBytes(CryptoService.STATIC_KEY) - both have to be the same bytes,
        // otherwise every issued token would be rejected by the API.
        Assert.Equal(CryptoService.STATIC_KEY, Encoding.UTF8.GetString(CryptoService.STATIC_KEY_BYTES));
    }

    #endregion

    #region uniqueness

    [Fact]
    public void GenerateToken_ForDifferentUsers_CarriesDifferentSubjects()
    {
        string first = _authService.GenerateToken(CreateChallenger(id: 1, name: "player-1"));
        string second = _authService.GenerateToken(CreateChallenger(id: 2, name: "player-2"));

        Assert.Equal("1", ReadPayload(first).GetProperty("sub").GetString());
        Assert.Equal("2", ReadPayload(second).GetProperty("sub").GetString());
    }

    [Fact]
    public void GenerateToken_CalledTwiceForTheSameUser_ProducesDifferentTokensWithTheSameIdentity()
    {
        var challenger = CreateChallenger();

        string first = _authService.GenerateToken(challenger);
        string second = _authService.GenerateToken(challenger);

        Assert.NotEqual(first, second);

        JsonElement firstPayload = ReadPayload(first);
        JsonElement secondPayload = ReadPayload(second);

        Assert.Equal(firstPayload.GetProperty("sub").GetString(), secondPayload.GetProperty("sub").GetString());
        Assert.Equal(firstPayload.GetProperty("unique_name").GetString(),
            secondPayload.GetProperty("unique_name").GetString());
        Assert.NotEqual(firstPayload.GetProperty("jti").GetString(), secondPayload.GetProperty("jti").GetString());
    }

    [Fact]
    public void GenerateToken_ForDifferentUsers_KeepsTheSameIssuerAudienceAndLifetime()
    {
        string first = _authService.GenerateToken(CreateChallenger(id: 1, name: "player-1"));
        string second = _authService.GenerateToken(CreateChallenger(id: 2, name: "player-2"));

        var firstParsed = new JwtSecurityTokenHandler().ReadJwtToken(first);
        var secondParsed = new JwtSecurityTokenHandler().ReadJwtToken(second);

        Assert.Equal(firstParsed.Issuer, secondParsed.Issuer);
        Assert.Equal(firstParsed.Audiences, secondParsed.Audiences);

        // The two tokens are issued one after the other, so their expiry may differ by one
        // second when a second boundary falls in between.
        Assert.InRange(Math.Abs((firstParsed.ValidTo - secondParsed.ValidTo).TotalSeconds), 0, 1);
    }

    #endregion

    #region edge cases

    [Fact]
    public void GenerateToken_WithUnicodeChallengerName_RoundTripsTheName()
    {
        const string name = "Рыцарь 🛡️ Ωμέγα";

        string token = _authService.GenerateToken(CreateChallenger(name: name));

        Assert.Equal(name, ReadPayload(token).GetProperty("unique_name").GetString());
    }

    [Fact]
    public void GenerateToken_WithJsonSpecialCharactersInTheName_RoundTripsTheName()
    {
        const string name = "a\"b\\c\nd\te<f>g&h";

        string token = _authService.GenerateToken(CreateChallenger(name: name));

        Assert.Equal(name, ReadPayload(token).GetProperty("unique_name").GetString());
    }

    [Fact]
    public void GenerateToken_WithTheMaximumAllowedNameLength_RoundTripsTheName()
    {
        // ChallengerModel.ChallengerName is limited to 200 characters by the database schema.
        string name = new('a', 200);

        string token = _authService.GenerateToken(CreateChallenger(name: name));

        Assert.Equal(name, ReadPayload(token).GetProperty("unique_name").GetString());
    }

    [Fact]
    public void GenerateToken_WithAnEmptyChallengerName_StillIssuesAValidToken()
    {
        string token = _authService.GenerateToken(CreateChallenger(name: string.Empty));

        var handler = CreateHandler();
        ClaimsPrincipal principal = handler.ValidateToken(token, BuildApiValidationParameters(), out _);

        Assert.NotNull(principal);
        Assert.Equal(string.Empty, ReadPayload(token).GetProperty("unique_name").GetString());
    }

    [Fact]
    public void GenerateToken_WithIdZero_StillCarriesTheSubjectClaim()
    {
        string token = _authService.GenerateToken(CreateChallenger(id: 0));

        Assert.Equal("0", ReadPayload(token).GetProperty("sub").GetString());
    }

    #endregion

    #region wiring

    [Fact]
    public void AuthService_ImplementsIAuthService()
    {
        Assert.IsAssignableFrom<IAuthService>(new AuthService());
    }

    [Fact]
    public void DependencyInjection_ResolvesIAuthServiceAsAuthService()
    {
        var services = new ServiceCollection();
        services.AddTransient<IAuthService, AuthService>();

        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<IAuthService>();

        Assert.IsType<AuthService>(service);
        Assert.False(string.IsNullOrWhiteSpace(service.GenerateToken(CreateChallenger())));
    }

    [Fact]
    public void Program_RegistersAuthServiceInTheDependencyInjectionContainer()
    {
        string source = File.ReadAllText(GetProgramCsPath());

        Assert.Matches(@"Add(?:Singleton|Scoped|Transient)<\s*IAuthService\s*,\s*AuthService\s*>\s*\(", source);
    }

    [Fact]
    public void Program_ConfiguresJwtBearerValidationForIssuerAudienceLifetimeAndSignature()
    {
        // Disabling any of these checks would let the API accept forged or replayed tokens.
        string source = File.ReadAllText(GetProgramCsPath());

        Assert.Matches(@"ValidateIssuer\s*=\s*true", source);
        Assert.Matches(@"ValidateAudience\s*=\s*true", source);
        Assert.Matches(@"ValidateLifetime\s*=\s*true", source);
        Assert.Matches(@"ValidateIssuerSigningKey\s*=\s*true", source);
    }

    private static string GetProgramCsPath()
    {
        var attribute = typeof(AuthServiceTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ApiProjectDirectory");

        Assert.NotNull(attribute);

        string path = Path.Combine(attribute!.Value ?? string.Empty, "Program.cs");

        Assert.True(File.Exists(path), $"Program.cs was not found at '{path}'.");
        return path;
    }

    #endregion
}
