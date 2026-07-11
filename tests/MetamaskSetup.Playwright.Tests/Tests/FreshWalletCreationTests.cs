namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Covers:
///   Scenario: Minimal fresh wallet creation
///   Scenario: Fresh wallet creation with caching enabled (default)
///   Scenario: Cache write sequence after fresh import
/// </summary>
[Collection(MetaMaskTestCollection.Name)]
public sealed class FreshWalletCreationTests : IAsyncLifetime
{
    private readonly MetaMaskExtensionFixture _extension;
    private IPlaywright _playwright = null!;

    public FreshWalletCreationTests(MetaMaskExtensionFixture extension)
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
    // Scenario: Minimal fresh wallet creation (caching explicitly disabled)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateWallet_CachingDisabled_ReturnsUsableContext()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        IBrowserContext? context = null;
        try
        {
            context = await service.SetupAsync();

            AssertContextIsReady(context);
            Assert.Empty(cache.CacheEntries());
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }

    // -------------------------------------------------------------------------
    // Scenario: Fresh wallet creation with caching enabled (default)
    // Scenario: Cache write sequence after fresh import
    //
    // After onboarding, the service runs Lock → Unlock → close → copy profile to
    // cache → relaunch → Unlock. We verify the returned context is usable AND
    // exactly one cache entry was written.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateWallet_CachingEnabled_WritesCacheEntryAndReturnsUsableContext()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        IBrowserContext? context = null;
        using var consoleCapture = new ConsoleCapture();
        try
        {
            context = await service.SetupAsync();

            AssertContextIsReady(context);

            Assert.Single(cache.CacheEntries());
            Assert.True(consoleCapture.Contains("Context saved to cache"),
                $"Expected 'Context saved to cache' in console output but got:\n{consoleCapture.Output}");
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void AssertContextIsReady(IBrowserContext context)
    {
        Assert.NotNull(context);
        Assert.NotEmpty(context.Pages);
        Assert.Contains("chrome-extension://", context.Pages[1].Url);
    }
}
