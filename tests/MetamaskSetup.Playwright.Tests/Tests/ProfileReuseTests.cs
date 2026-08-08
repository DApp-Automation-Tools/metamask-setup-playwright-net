namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Covers:
///   Scenario: Provide a pre-existing profile directory
///   Scenario: Cache hit — unlock only
/// </summary>
[Collection(MetaMaskTestCollection.Name)]
public sealed class ProfileReuseTests : IAsyncLifetime
{
    private readonly MetaMaskExtensionFixture _extension;
    private IPlaywright _playwright = null!;

    public ProfileReuseTests(MetaMaskExtensionFixture extension)
    {
        _extension = extension;
    }

    public async Task InitializeAsync()
    {
        _playwright = await PlaywrightFactory.CreateAsync();
    }

    public Task DisposeAsync()
    {
        _playwright.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FromUserProfile_ExistingCacheEntry_UnlocksWithoutOnboarding()
    {
        using var cache = new IsolatedCacheDirectory();

        var freshService = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var freshResult = await freshService.SetupAsync();
        await freshService.CleanupAsync(freshResult);

        var entries = cache.CacheEntries();
        Assert.Single(entries);
        var profileDirectory = entries[0];

        var reuseService = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .FromUserProfile(profileDirectory);

        IBrowserContext? reuseContext = null;
        try
        {
            var reuseResult = await reuseService.SetupAsync();
            reuseContext = reuseResult.Context;

            await MetaMaskAssertions.AssertContextReadyAsync(reuseContext);
            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (reuseContext != null)
                await reuseService.CleanupAsync(reuseContext);
        }
    }

    [Fact]
    public async Task CacheHit_SecondCallWithSameParameters_SkipsOnboardingAndCacheWrite()
    {
        using var cache = new IsolatedCacheDirectory();

        MetaMaskSetupService BuildService() =>
            new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
                .WithPassword(TestConfig.Password)
                .WithSeedPhrase(TestConfig.SeedPhrase)
                .WithContextCachePath(cache.Path)
                .UseContextCacheIfExists(true)
                .WithExtensionSaveDelayMs(500);

        var service1 = BuildService();
        var result1 = await service1.SetupAsync();
        await service1.CleanupAsync(result1);

        Assert.Single(cache.CacheEntries());

        var service2 = BuildService();
        IBrowserContext? context2 = null;
        try
        {
            var result2 = await service2.SetupAsync();
            context2 = result2.Context;

            await MetaMaskAssertions.AssertContextReadyAsync(context2);
            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (context2 != null)
                await service2.CleanupAsync(context2);
        }
    }
}
