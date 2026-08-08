namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Covers:
///   Scenario: Cache key is determined by all setup parameters
///   Scenario: Caching disabled — no cache read or write
/// </summary>
[Collection(MetaMaskTestCollection.Name)]
public sealed class CacheManagementTests : IAsyncLifetime
{
    private readonly MetaMaskExtensionFixture _extension;
    private IPlaywright _playwright = null!;

    public CacheManagementTests(MetaMaskExtensionFixture extension)
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
    public async Task CacheKey_DifferentPasswords_ProduceDifferentCacheEntries()
    {
        using var cache = new IsolatedCacheDirectory();

        async Task RunFreshSetup(string password)
        {
            var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
                .WithPassword(password)
                .WithSeedPhrase(TestConfig.SeedPhrase)
                .WithContextCachePath(cache.Path)
                .UseContextCacheIfExists(true)
                .WithExtensionSaveDelayMs(500);

            var result = await service.SetupAsync();
            await service.CleanupAsync(result);
        }

        await RunFreshSetup("PasswordAlpha1!");
        await RunFreshSetup("PasswordBeta2!");

        var entries = cache.CacheEntries();
        Assert.Equal(2, entries.Length);
        Assert.NotEqual(
            Path.GetFileName(entries[0]),
            Path.GetFileName(entries[1]));

        foreach (var entry in entries)
            Assert.True(Directory.Exists(Path.Combine(entry, "Default")),
                $"Cache entry '{Path.GetFileName(entry)}' must contain a Chromium profile.");
    }

    [Fact]
    public async Task CacheKey_DifferentSeedPhrases_ProduceDifferentCacheEntries()
    {
        using var cache = new IsolatedCacheDirectory();

        var service1 = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase("test test test test test test test test test test test junk")
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var result1 = await service1.SetupAsync();
        await service1.CleanupAsync(result1);

        var service2 = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about")
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var result2 = await service2.SetupAsync();
        await service2.CleanupAsync(result2);

        var entries = cache.CacheEntries();
        Assert.Equal(2, entries.Length);

        foreach (var entry in entries)
            Assert.True(Directory.Exists(Path.Combine(entry, "Default")),
                $"Cache entry '{Path.GetFileName(entry)}' must contain a Chromium profile.");
    }

    [Fact]
    public async Task CachingDisabled_NoCacheEntryCreated()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        IBrowserContext? context = null;
        try
        {
            var result = await service.SetupAsync();
            context = result.Context;

            await MetaMaskAssertions.AssertContextReadyAsync(context);
            Assert.Empty(cache.CacheEntries());
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }

    [Fact]
    public async Task CachingDisabled_ExistingCacheIgnored_FullOnboardingRuns()
    {
        using var cache = new IsolatedCacheDirectory();

        var warmup = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var warmupResult = await warmup.SetupAsync();
        await warmup.CleanupAsync(warmupResult);
        Assert.Single(cache.CacheEntries());

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        IBrowserContext? context = null;
        try
        {
            var result = await service.SetupAsync();
            context = result.Context;

            await MetaMaskAssertions.AssertContextReadyAsync(context);
            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }
}
