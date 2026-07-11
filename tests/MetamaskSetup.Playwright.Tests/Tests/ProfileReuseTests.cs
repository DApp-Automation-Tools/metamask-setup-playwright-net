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

    // -------------------------------------------------------------------------
    // Scenario: Provide a pre-existing profile directory
    //
    // Step 1 — write a cache entry via fresh onboarding.
    // Step 2 — pass the cache entry directory to FromUserProfile().
    //          The library detects a "Default" subdirectory → treats it as a profile.
    //          Only UnlockWalletAsync runs; no onboarding, no network, no account steps.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FromUserProfile_ExistingCacheEntry_UnlocksWithoutOnboarding()
    {
        using var cache = new IsolatedCacheDirectory();

        // --- Step 1: fresh setup to populate the cache ---
        var freshService = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var freshContext = await freshService.SetupAsync();
        await freshService.CleanupAsync(freshContext);

        var entries = cache.CacheEntries();
        Assert.Single(entries);
        var profileDirectory = entries[0];

        // --- Step 2: reuse the cache entry via FromUserProfile ---
        var reuseService = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .FromUserProfile(profileDirectory);

        IBrowserContext? reuseContext = null;
        using var consoleCapture = new ConsoleCapture();
        try
        {
            reuseContext = await reuseService.SetupAsync();

            Assert.NotNull(reuseContext);
            Assert.NotEmpty(reuseContext.Pages);
            Assert.Contains("chrome-extension://", reuseContext.Pages.First().Url);

            Assert.False(consoleCapture.Contains("Context saved to cache"),
                "FromUserProfile path should not write a cache entry.");
        }
        finally
        {
            if (reuseContext != null)
                await reuseService.CleanupAsync(reuseContext);
        }
    }

    // -------------------------------------------------------------------------
    // Scenario: Cache hit — unlock only
    //
    // Two consecutive SetupAsync calls with identical parameters on separate service
    // instances. The second call hits the cache written by the first.
    // -------------------------------------------------------------------------

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

        // --- First call: fresh onboarding + cache write ---
        var service1 = BuildService();
        var context1 = await service1.SetupAsync();
        await service1.CleanupAsync(context1);

        Assert.Single(cache.CacheEntries());

        // --- Second call: should hit the cache ---
        var service2 = BuildService();
        IBrowserContext? context2 = null;
        using var consoleCapture = new ConsoleCapture();
        try
        {
            context2 = await service2.SetupAsync();

            Assert.NotNull(context2);
            Assert.NotEmpty(context2.Pages);
            Assert.Contains("chrome-extension://", context2.Pages.First().Url);

            // Cache hit path does not write a second entry
            Assert.Single(cache.CacheEntries());
            Assert.False(consoleCapture.Contains("Context saved to cache"),
                "Cache hit path should not write a second cache entry.");
        }
        finally
        {
            if (context2 != null)
                await service2.CleanupAsync(context2);
        }
    }
}
