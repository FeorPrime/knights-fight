using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Entities;
using KnightFight.Api.Models.Responses;
using KnightFight.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KnightFight.Api.Tests;

public class AvatarContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AvatarContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAvatar_UnknownChallenger_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/challengers/999999/avatar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAvatar_ChallengerWithCustomBlob_ReturnsPng()
    {
        var png = MakePng();
        var id = await SeedChallenger("AvatarCustom", c =>
        {
            c.AvatarBlob = png;
            c.DefaultAvatarIndex = null;
        });

        var response = await _client.GetAsync($"/api/challengers/{id}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, bytes);
    }

    [Fact]
    public async Task GetAvatar_ChallengerWithDefault_ReturnsTheBundledAvatar()
    {
        var id = await SeedChallenger("AvatarDefault", c =>
        {
            c.AvatarBlob = null;
            c.DefaultAvatarIndex = 3;
        });

        var response = await _client.GetAsync($"/api/challengers/{id}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
    }

    [Fact]
    public async Task GetAvatar_SetsAnEtag()
    {
        var id = await SeedChallenger("AvatarEtag", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
        });

        var response = await _client.GetAsync($"/api/challengers/{id}/avatar");

        Assert.NotNull(response.Headers.ETag);
        Assert.NotEmpty(response.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task CreateChallenger_AssignsARandomDefaultAvatar()
    {
        var name = $"CreatedWithDefault{Guid.NewGuid():N}";

        var response = await EncryptedRequestFactory.PostEncryptedAsync(
            _client,
            "/api/challenger",
            JsonSerializer.Serialize(new { ChallengerName = name, Secret = "s3cret" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = body.GetProperty("resultValue").GetProperty("id").GetInt64();

        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var challenger = await ctx.Challengers.SingleAsync(c => c.Id == id);

        Assert.Null(challenger.AvatarBlob);
        Assert.NotNull(challenger.DefaultAvatarIndex);
        Assert.InRange(challenger.DefaultAvatarIndex!.Value, 0, 9);

        // the assigned default is immediately fetchable
        var avatar = await _client.GetAsync($"/api/challengers/{id}/avatar");
        Assert.Equal(HttpStatusCode.OK, avatar.StatusCode);
        Assert.Equal("image/png", avatar.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetLeaderboard_IncludesTheAvatarUrl()
    {
        var id = await SeedChallenger("LeaderboardAvatar", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
            c.Wins = 5;
        });

        var response = await _client.GetAsync("/api/leaderboard?limit=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<RankedChallengerResponse>>(JsonOptions);
        Assert.NotNull(items);

        var item = items.Single(i => i.Id == id);
        Assert.Equal($"/api/challengers/{id}/avatar", item.AvatarUrl);
    }

    [Fact]
    public async Task GetChallengers_IncludesTheAvatarUrl()
    {
        var id = await SeedChallenger("NewcomerAvatar", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
        });

        var response = await _client.GetAsync("/api/challengers?page=0&page_size=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedChallengersResponse>(JsonOptions);
        Assert.NotNull(page);

        var item = page.Items.Single(i => i.Id == id);
        Assert.Equal($"/api/challengers/{id}/avatar", item.AvatarUrl);
    }

    [Fact]
    public async Task GetLobby_IncludesTheAvatarUrl()
    {
        var id = await SeedChallenger("LobbyAvatarKnight", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
        });

        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        await seedsProvider.ForceAdd("LobbyAvatarKnight", "SEED-AVATAR");

        var response = await _client.GetAsync("/api/lobby");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<LobbyStatusResponse>(JsonOptions);
        Assert.NotNull(status);

        var player = status.Players.Single(p => p.Name == "LobbyAvatarKnight");
        Assert.Equal($"/api/challengers/{id}/avatar", player.AvatarUrl);
    }

    [Fact]
    public async Task UploadAvatar_WithoutAToken_IsUnauthorized()
    {
        var id = await SeedChallenger("UploadNoToken", c => c.DefaultAvatarIndex = 1);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MakePng()), "avatar", "avatar.png");

        var response = await _client.PostAsync($"/api/challengers/{id}/avatar", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WithAForeignToken_IsForbidden()
    {
        var ownerId = await SeedChallenger("UploadOwner", c => c.DefaultAvatarIndex = 1);
        var otherId = await SeedChallenger("UploadOther", c => c.DefaultAvatarIndex = 2);

        var token = await TokenFor("UploadOwner");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MakePng()), "avatar", "avatar.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/challengers/{otherId}/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WithATextFile_IsBadRequest()
    {
        var name = $"UploadText{Guid.NewGuid():N}";
        var id = await SeedChallenger(name, c => c.DefaultAvatarIndex = 1);
        var token = await TokenFor(name);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("not an image"u8.ToArray()), "avatar", "avatar.txt");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/challengers/{id}/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WithAPng_StoresItAndClearsTheDefault()
    {
        var name = $"UploadOk{Guid.NewGuid():N}";
        var id = await SeedChallenger(name, c => c.DefaultAvatarIndex = 4);
        var token = await TokenFor(name);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MakePng()), "avatar", "avatar.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/challengers/{id}/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var challenger = await ctx.Challengers.SingleAsync(c => c.Id == id);

        Assert.NotNull(challenger.AvatarBlob);
        Assert.Null(challenger.DefaultAvatarIndex);

        // and it is what the avatar route now serves
        var served = await _client.GetAsync($"/api/challengers/{id}/avatar");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    private async Task<long> SeedChallenger(string name, Action<ChallengerModel> configure)
    {
        await _factory.SeedDatabaseAsync(ctx =>
        {
            if (ctx.Challengers.Any(c => c.ChallengerName == name)) return;

            var entity = new ChallengerModel
            {
                ChallengerName = name,
                Secret = "hash"
            };
            configure(entity);
            ctx.Challengers.Add(entity);
        });

        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var seeded = await ctx.Challengers.SingleAsync(c => c.ChallengerName == name);
        return seeded.Id;
    }

    private async Task<string> TokenFor(string challengerName)
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var challenger = await ctx.Challengers.SingleAsync(c => c.ChallengerName == challengerName);

        return _factory.Services.GetRequiredService<IAuthService>().GenerateToken(challenger);
    }

    private static byte[] MakePng()
    {
        using var bitmap = new SkiaSharp.SKBitmap(50, 50);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.DodgerBlue);
        }

        using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
