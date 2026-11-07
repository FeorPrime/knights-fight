using System.Reflection;
using KnightFight.Api.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnightFight.Api.Tests;

/// <summary>
/// Tests for the SINGLETON lifetime of <see cref="OtpSeedsProvider"/>.
/// <para>
/// Production wiring: <c>Program.cs</c> registers
/// <c>AddSingleton&lt;IOtpSeedsProvider, OtpSeedsProvider&gt;()</c>, so the whole application
/// shares one instance and one seed store. These tests pin that contract down, because the
/// singleton is what makes the seed written during login (AuthGetTokens) visible to the
/// verification endpoint (TestOtp) - and what makes a per-request store impossible.
/// </para>
/// </summary>
public class OtpSeedsProviderSingletonTests
{
    private const string CHALLENGER_NOT_FOUND = "Challenger not found";

    /// <summary>
    /// The API project directory, injected at build time - see the
    /// <c>ApiProjectDirectory</c> assembly metadata in the test project file.
    /// </summary>
    private static string ApiProjectDirectory
    {
        get
        {
            var attribute = typeof(OtpSeedsProviderSingletonTests).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "ApiProjectDirectory");

            Assert.NotNull(attribute);
            return attribute!.Value ?? string.Empty;
        }
    }

    /// <summary>
    /// Builds the container exactly the way <c>Program.cs</c> does.
    /// </summary>
    private static ServiceProvider BuildProductionContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOtpSeedsProvider, OtpSeedsProvider>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DependencyInjection_ResolvesTheVerySameInstanceEveryTime()
    {
        using var provider = BuildProductionContainer();

        var first = provider.GetRequiredService<IOtpSeedsProvider>();
        var second = provider.GetRequiredService<IOtpSeedsProvider>();

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.IsType<OtpSeedsProvider>(first);
    }

    [Fact]
    public void DependencyInjection_SharesTheSameInstanceAcrossScopes()
    {
        using var provider = BuildProductionContainer();
        using var scopeOne = provider.CreateScope();
        using var scopeTwo = provider.CreateScope();

        var fromScopeOne = scopeOne.ServiceProvider.GetRequiredService<IOtpSeedsProvider>();
        var fromScopeTwo = scopeTwo.ServiceProvider.GetRequiredService<IOtpSeedsProvider>();

        Assert.Same(fromScopeOne, fromScopeTwo);
    }

    [Fact]
    public async Task DependencyInjection_SharedInstance_SharesSeedStateBetweenResolutions()
    {
        // The real-world contract: the login flow writes the seed through one resolution,
        // the verification endpoint reads it through another one.
        using var provider = BuildProductionContainer();

        var writer = provider.GetRequiredService<IOtpSeedsProvider>();
        var reader = provider.GetRequiredService<IOtpSeedsProvider>();

        var added = await writer.ForceAdd("player-1", "JBSWY3DPEHPK3PXP");
        Assert.True(added.IsSuccess);

        var stored = await reader.Get("player-1");
        Assert.True(stored.IsSuccess);
        Assert.Equal("JBSWY3DPEHPK3PXP", stored.ResultValue);

        // ...and a removal through one resolution is visible to the other one as well.
        var removed = await reader.Remove("player-1");
        Assert.True(removed.IsSuccess);

        var gone = await writer.Get("player-1");
        Assert.True(gone.IsError);
        Assert.Equal(CHALLENGER_NOT_FOUND, gone.Problem);
    }

    [Fact]
    public async Task SingletonInstance_ConcurrentUsers_KeepTheirOwnSeeds()
    {
        // One shared instance serves every request at the same time: seeds of different
        // users must not bleed into each other.
        using var provider = BuildProductionContainer();
        var seedsProvider = provider.GetRequiredService<IOtpSeedsProvider>();

        const int users = 500;
        // Task.Run is required - the provider methods are synchronous under the hood, so a
        // plain Task.WhenAll would run them sequentially and never test any concurrency.
        await Task.WhenAll(Enumerable.Range(0, users)
            .Select(i => Task.Run(() => seedsProvider.ForceAdd($"player-{i}", $"SEED-{i}"))));

        var reads = await Task.WhenAll(Enumerable.Range(0, users)
            .Select(i => Task.Run(() => seedsProvider.Get($"player-{i}"))));

        for (var i = 0; i < users; i++)
        {
            Assert.True(reads[i].IsSuccess);
            Assert.Equal($"SEED-{i}", reads[i].ResultValue);
        }
    }

    [Fact]
    public void Program_RegistersOtpSeedsProviderAsSingleton()
    {
        // Guards the lifetime itself: a change to AddTransient/AddScoped in Program.cs would
        // silently break the login -> verification flow, so it has to fail here first.
        string programPath = Path.Combine(ApiProjectDirectory, "Program.cs");

        Assert.True(File.Exists(programPath),
            $"Program.cs was not found at '{programPath}', the singleton registration cannot be verified.");

        string source = File.ReadAllText(programPath);

        Assert.Matches(@"AddSingleton<\s*IOtpSeedsProvider\s*,\s*OtpSeedsProvider\s*>\s*\(", source);
        Assert.DoesNotMatch(@"Add(?:Transient|Scoped)<\s*IOtpSeedsProvider\b", source);
    }
}
