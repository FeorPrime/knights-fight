using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Entities;
using KnightFight.Api.Models.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnightFight.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            // Bind the UDP listener to an ephemeral port. The service claims its port while
            // starting up and lets a refused bind fail the app, so a test host that kept the
            // documented 5050 would fight a locally running API and every Fact here would fail.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UdpSettings:Port"] = "0",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<KfContext>) ||
                d.ServiceType == typeof(IDbContextFactory<KfContext>) ||
                d.ServiceType == typeof(KfContext) ||
                d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                d.ServiceType.Namespace?.StartsWith("Npgsql.EntityFrameworkCore") == true).ToList();

            foreach (var d in descriptors)
            {
                services.Remove(d);
            }

            services.AddDbContextFactory<KfContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }

    public async Task SeedDatabaseAsync(Action<KfContext> seedAction)
    {
        using var scope = Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        seedAction(ctx);
        await ctx.SaveChangesAsync();
    }
}

public class EndpointContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EndpointContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetChallengers_ReturnsExpectedShapeAndPagination()
    {
        await _factory.SeedDatabaseAsync(ctx =>
        {
            if (!ctx.Challengers.Any(c => c.ChallengerName == "KnightAlpha"))
            {
                ctx.Challengers.Add(new ChallengerModel
                {
                    ChallengerName = "KnightAlpha",
                    Secret = "hash1",
                    Wins = 5,
                    Loses = 2,
                    Draws = 1,
                    Challenges = 8
                });
                ctx.Challengers.Add(new ChallengerModel
                {
                    ChallengerName = "KnightBeta",
                    Secret = "hash2",
                    Wins = 10,
                    Loses = 1,
                    Draws = 0,
                    Challenges = 11
                });
            }
        });

        // Test snake_case query param
        var response = await _client.GetAsync("/api/challengers?page=0&page_size=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<PagedChallengersResponse>(JsonOptions);
        Assert.NotNull(content);
        Assert.NotNull(content.Items);
        Assert.True(content.TotalCount >= 2);
        Assert.Equal(0, content.Page);
        Assert.Equal(5, content.PageSize);

        var firstItem = content.Items[0];
        Assert.True(firstItem.Id > 0);
        Assert.False(string.IsNullOrEmpty(firstItem.Name));
        Assert.NotEqual(default, firstItem.RegisteredAt);
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsRankedChallengers()
    {
        await _factory.SeedDatabaseAsync(ctx =>
        {
            if (!ctx.Challengers.Any(c => c.ChallengerName == "Leader1"))
            {
                ctx.Challengers.Add(new ChallengerModel
                {
                    ChallengerName = "Leader1",
                    Secret = "h1",
                    Wins = 10,
                    Loses = 2,
                    Draws = 1 // Score = 31
                });
                ctx.Challengers.Add(new ChallengerModel
                {
                    ChallengerName = "Leader2",
                    Secret = "h2",
                    Wins = 5,
                    Loses = 5,
                    Draws = 0 // Score = 15
                });
            }
        });

        var response = await _client.GetAsync("/api/leaderboard?limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<RankedChallengerResponse>>(JsonOptions);
        Assert.NotNull(items);
        Assert.NotEmpty(items);

        // Check ranking and scores are ordered descending
        for (int i = 0; i < items.Count; i++)
        {
            Assert.Equal(i + 1, items[i].Rank);
            if (i > 0)
            {
                Assert.True(items[i - 1].Score >= items[i].Score);
            }
        }
    }

    [Fact]
    public async Task GetChallengersStream_ConnectsAndStreamsEvents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/challengers/stream");
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        // Allow handler to enter the enumeration loop
        await Task.Delay(100, cts.Token);

        // Publish an event via notification service
        var notificationService = _factory.Services.GetRequiredService<INewcomerNotificationService>();
        await notificationService.PublishAsync(new ChallengerNewcomerEvent(999, "StreamKnight", DateTime.UtcNow, null));

        var line = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(line);
        Assert.StartsWith("data: ", line);
        Assert.Contains("StreamKnight", line);
    }

    [Fact]
    public async Task GetMatches_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/matches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<List<MatchDto>>(JsonOptions);
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetLobby_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/lobby");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<LobbyStatusResponse>(JsonOptions);
        Assert.NotNull(content);
        Assert.NotNull(content.Players);
        Assert.NotNull(content.Queue);
    }

    [Fact]
    public async Task Lobby_LoggedInUserAppearsUnready_AndCanBeMarkedReadyViaPost()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var authService = _factory.Services.GetRequiredService<IAuthService>();
        await seedsProvider.ForceAdd("LobbyKnight", "SEED123");

        var token = authService.GenerateToken(new ChallengerModel
        {
            Id = 42,
            ChallengerName = "LobbyKnight",
            Secret = "secret"
        });

        // 1. Get lobby: challenger appears with isReady = false / unready
        var response1 = await _client.GetAsync("/api/lobby");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var content1 = await response1.Content.ReadFromJsonAsync<LobbyStatusResponse>(JsonOptions);
        Assert.NotNull(content1);
        Assert.Contains(content1.Players, p => p.Name == "LobbyKnight" && !p.IsReady && p.Status == "unready");

        // 2. Unauthenticated POST /api/lobby returns 401 Unauthorized
        var joinReq = new LobbyJoinRequest("LobbyKnight", true);
        var unauthResponse = await _client.PostAsJsonAsync("/api/lobby", joinReq);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 3. Authenticated POST /api/lobby marks challenger ready
        var authRequest = new HttpRequestMessage(HttpMethod.Post, "/api/lobby")
        {
            Content = JsonContent.Create(joinReq)
        };
        authRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var postResponse = await _client.SendAsync(authRequest);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        // 4. Get lobby: challenger appears with isReady = true / ready
        var response2 = await _client.GetAsync("/api/lobby");
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var content2 = await response2.Content.ReadFromJsonAsync<LobbyStatusResponse>(JsonOptions);
        Assert.NotNull(content2);
        Assert.Contains(content2.Players, p => p.Name == "LobbyKnight" && p.IsReady && p.Status == "ready");

        // 5. Authenticated POST packet for non-logged-in challenger returns 404
        var badReq = new LobbyJoinRequest("NonExistentKnight", true);
        var badAuthRequest = new HttpRequestMessage(HttpMethod.Post, "/api/lobby")
        {
            Content = JsonContent.Create(badReq)
        };
        badAuthRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var badPostResponse = await _client.SendAsync(badAuthRequest);
        Assert.Equal(HttpStatusCode.NotFound, badPostResponse.StatusCode);
    }

    [Fact]
    public async Task Lobby_TwoReadyChallengers_StartAMatchAndReportItsId()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var authService = _factory.Services.GetRequiredService<IAuthService>();

        await seedsProvider.ForceAdd("MatchKnightA", "SEED-A");
        await seedsProvider.ForceAdd("MatchKnightB", "SEED-B");

        var token = authService.GenerateToken(new ChallengerModel
        {
            Id = 101,
            ChallengerName = "MatchKnightA",
            Secret = "secret"
        });

        // 1. The first challenger becomes ready - nobody to play against yet.
        var firstResponse = await PostLobbyAsync(token, "MatchKnightA", true);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<LobbyJoinResponse>(JsonOptions);
        Assert.NotNull(firstBody);
        Assert.True(firstBody.Success);
        Assert.Null(firstBody.MatchId);

        // 2. The second challenger becomes ready - a match starts.
        var secondResponse = await PostLobbyAsync(token, "MatchKnightB", true);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<LobbyJoinResponse>(JsonOptions);
        Assert.NotNull(secondBody);
        Assert.True(secondBody.Success);
        Assert.NotNull(secondBody.MatchId);
        Assert.NotNull(secondBody.Opponent);
        Assert.Equal("MatchKnightA", secondBody.Opponent);
    }

    private async Task<HttpResponseMessage> PostLobbyAsync(string token, string challengerName, bool isReady)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/lobby")
        {
            Content = JsonContent.Create(new LobbyJoinRequest(challengerName, isReady))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task GetMatches_ListsTheStartedMatch()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var authService = _factory.Services.GetRequiredService<IAuthService>();
        var matchService = _factory.Services.GetRequiredService<ILobbyMatchService>();

        await seedsProvider.ForceAdd("ListedKnightA", "SEED-A");
        await seedsProvider.ForceAdd("ListedKnightB", "SEED-B");
        await seedsProvider.SetReady("ListedKnightA", true);
        await seedsProvider.SetReady("ListedKnightB", true);

        var outcome = await matchService.TryStartMatchesAsync();
        var match = Assert.Single(outcome.StartedMatches);

        var response = await _client.GetAsync("/api/matches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<List<MatchDto>>(JsonOptions);
        Assert.NotNull(content);
        Assert.Contains(content, m => m.Id == match.Id.ToString()
                                      && m.Challenger1 == "ListedKnightA"
                                      && m.Challenger2 == "ListedKnightB"
                                      && m.Status == "InProgress");
    }

    [Fact]
    public async Task GetMatchById_ReturnsTheMatch_And404sForAnUnknownId()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var matchService = _factory.Services.GetRequiredService<ILobbyMatchService>();

        await seedsProvider.ForceAdd("ByIdKnightA", "SEED-A");
        await seedsProvider.ForceAdd("ByIdKnightB", "SEED-B");
        await seedsProvider.SetReady("ByIdKnightA", true);
        await seedsProvider.SetReady("ByIdKnightB", true);

        var outcome = await matchService.TryStartMatchesAsync();
        var match = Assert.Single(outcome.StartedMatches);

        var found = await _client.GetAsync($"/api/matches/{match.Id}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var body = await found.Content.ReadFromJsonAsync<MatchDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(match.Id.ToString(), body.Id);

        var missing = await _client.GetAsync($"/api/matches/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
