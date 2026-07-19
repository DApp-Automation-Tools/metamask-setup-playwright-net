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

    // -------------------------------------------------------------------------
    // Scenario: Cache key is determined by all setup parameters
    //
    // Two fresh setups with different passwords run into the same cache root.
    // Because the password is part of the SHA-256 input, they must produce
    // different subdirectories (different cache keys).
    // -------------------------------------------------------------------------

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

            var context = await service.SetupAsync();
            await service.CleanupAsync(context);
        }

        await RunFreshSetup("PasswordAlpha1!");
        await RunFreshSetup("PasswordBeta2!");

        // Different passwords → different cache keys → two separate entry directories
        var entries = cache.CacheEntries();
        Assert.Equal(2, entries.Length);
        Assert.NotEqual(
            Path.GetFileName(entries[0]),
            Path.GetFileName(entries[1]));

        // Each entry must be a valid Chromium profile
        foreach (var entry in entries)
            Assert.True(Directory.Exists(Path.Combine(entry, "Default")),
                $"Cache entry '{Path.GetFileName(entry)}' must contain a Chromium profile.");
    }

    // -------------------------------------------------------------------------
    // Scenario: Cache key is determined by all setup parameters (seed phrase variant)
    // -------------------------------------------------------------------------

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

        var context1 = await service1.SetupAsync();
        await service1.CleanupAsync(context1);

        var service2 = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about")
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var context2 = await service2.SetupAsync();
        await service2.CleanupAsync(context2);

        var entries = cache.CacheEntries();
        Assert.Equal(2, entries.Length);

        foreach (var entry in entries)
            Assert.True(Directory.Exists(Path.Combine(entry, "Default")),
                $"Cache entry '{Path.GetFileName(entry)}' must contain a Chromium profile.");
    }

    // -------------------------------------------------------------------------
    // Scenario: Caching disabled — no cache read or write
    // -------------------------------------------------------------------------

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
            context = await service.SetupAsync();

            // Wallet must be unlocked — setup completed successfully even without caching
            await MetaMaskAssertions.AssertContextReadyAsync(context);

            // No cache entry must have been written to disk
            Assert.Empty(cache.CacheEntries());
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }

    // -------------------------------------------------------------------------
    // Scenario: Caching disabled — existing cache is not read even if present
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CachingDisabled_ExistingCacheIgnored_FullOnboardingRuns()
    {
        using var cache = new IsolatedCacheDirectory();

        // Step 1: populate the cache
        var warmup = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var warmupContext = await warmup.SetupAsync();
        await warmup.CleanupAsync(warmupContext);
        Assert.Single(cache.CacheEntries());

        // Step 2: same parameters, caching disabled → onboarding runs again, no new entry written
        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        IBrowserContext? context = null;
        try
        {
            context = await service.SetupAsync();

            // Wallet must be unlocked — fresh onboarding completed successfully
            await MetaMaskAssertions.AssertContextReadyAsync(context);

            // Cache count must not have grown — UseContextCacheIfExists(false) skips the write
            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }
}
