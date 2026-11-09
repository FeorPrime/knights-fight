using System.Net.Sockets;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnightFight.Api.Tests;

/// <summary>
/// The API refuses to start when the game command port is already owned by another listener.
/// A UDP port has one global namespace, so a system service or a second API instance can
/// already hold it — serving HTTP over a dead game command channel is worse than failing.
/// </summary>
public class ClaimedUdpPortWebApplicationFactory : WebApplicationFactory<Program>
{
    // The test squats this port before asking the API to start, so the API has to lose the
    // bind. Any port the test owns works — the documented 5050 is already owned on Windows.
    public static readonly int _gameCommandPort = 21543;

    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UdpSettings:Port"] = _gameCommandPort.ToString(),
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<KfContext>) ||
                d.ServiceType == typeof(IDbContextFactory<KfContext>) ||
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
}

public class UdpStartupContractTests
{
    [Fact]
    public void StartApi_UdpGameCommandPortAlreadyClaimed_TheApiDoesNotStart()
    {
        // The squatter plays the part of the system service, or of a second API instance,
        // that already owns the port — the same shape the host sees at startup.
        using var squatter = new UdpClient(ClaimedUdpPortWebApplicationFactory._gameCommandPort);

        var factory = new ClaimedUdpPortWebApplicationFactory();

        var failure = Assert.Throws<SocketException>(() => factory.CreateClient());

        // The failure is the bind, not the database swap or an unrelated startup error. The
        // operating system localizes the message, so the error code is what this can assert.
        Assert.Equal(SocketError.AddressAlreadyInUse, failure.SocketErrorCode);
    }

    [Fact]
    public void StartApi_FreeUdpGameCommandPort_StartsAndOwnsThePort()
    {
        var factory = new ClaimedUdpPortWebApplicationFactory();
        _ = factory.CreateClient();

        // The listener holds the port for the whole run: a second one cannot take it,
        // which is what makes the game command channel real rather than merely reported.
        var collision = Assert.Throws<SocketException>(
            () => new UdpClient(ClaimedUdpPortWebApplicationFactory._gameCommandPort));
        Assert.Equal(SocketError.AddressAlreadyInUse, collision.SocketErrorCode);
    }
}
