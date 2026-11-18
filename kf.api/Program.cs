using System.Text;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Infrastructure.Services.Crypto;
using KnightFight.Api.Infrastructure.Services.Images;
using KnightFight.Game.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddSingleton<IOtpService, OtpService>();
builder.Services.AddSingleton<IOtpSeedsProvider, OtpSeedsProvider>();
builder.Services.AddSingleton<INewcomerNotificationService, NewcomerNotificationService>();
builder.Services.AddSingleton<IDefaultAvatarProvider, DefaultAvatarProvider>();
builder.Services.AddSingleton<IImageService, AvatarImageService>();

// Game services - the API is the first consumer of the kf.game engine.
builder.Services.AddSingleton<IMatchmaker, Matchmaker>();
builder.Services.AddSingleton<IGameManager>(_ => new GameManager());
builder.Services.AddSingleton<ILobbyMatchService, LobbyMatchService>();

// Command processing services
builder.Services.AddSingleton<ICommandProcessor, CommandProcessor>();
builder.Services.AddSingleton<IUdpCommandService, UdpCommandService>();
builder.Services.AddHostedService(provider =>
    (UdpCommandService)provider.GetRequiredService<IUdpCommandService>());

// Turn clock — paces the engine's turn loop over live matches
builder.Services.AddSingleton<ITurnService, TurnService>();
builder.Services.AddHostedService(provider =>
    (TurnService)provider.GetRequiredService<ITurnService>());

builder.Services.AddDbContextFactory<KfContext>(opt =>
{
    opt.UseNpgsql(BuildConnectionString(), BuildNpgsqlOptions());
});

builder.Services.RegisterApiEndpoints();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = AuthService.ISSUER,
            ValidAudience = AuthService.AUDIENCE,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CryptoService.STATIC_KEY)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

ApplyMigrations(app);

app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapScalarApiReference("/api/scalar", options => options.WithOpenApiRoutePattern("/api/openapi/{documentName}.json"));
app.UseSwaggerUI(options => options.SwaggerEndpoint("/api/openapi/v1.json", "KnightFight API v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapApiEndpoints();

app.Run();
return;

string BuildConnectionString()
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    var csb = BuildNpgsqlConnectionString();
    return cs ?? csb.ConnectionString;
}

NpgsqlConnectionStringBuilder BuildNpgsqlConnectionString()
{
    var npgsqlConnectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? builder.Configuration["POSTGRES_HOST"] ?? "localhost",
        Port = int.TryParse(Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? builder.Configuration["POSTGRES_PORT"], out var p) ? p : 5432,
        Database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? builder.Configuration["POSTGRES_DB"] ?? "kf_db",
        Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? builder.Configuration["POSTGRES_USER"] ?? "kf_user",
        Password = Environment.GetEnvironmentVariable("POSTGRES_PW") ?? builder.Configuration["POSTGRES_PW"] ?? throw new Exception("POSTGRES_PW Is not set"),
    };
    return npgsqlConnectionStringBuilder;
}

Action<NpgsqlDbContextOptionsBuilder>? BuildNpgsqlOptions()
{
    return npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    };
}

void ApplyMigrations(WebApplication webApplication)
{
    using var scope = webApplication.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<KfContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }
}

public partial class Program { }
