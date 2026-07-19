namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Verifies that MetaMask settings survive the full cache round-trip:
///   1. Fresh setup with a setting (network, account, …) → writes cache → cleanup.
///   2. Reload from the same cache entry → wallet unlocks → setting is still in place.
///
/// Each test uses two separate MetaMaskSetupService instances with identical parameters
/// so the cache key matches and the second call is a guaranteed cache hit.
/// </summary>
[Collection(MetaMaskTestCollection.Name)]
public sealed class CachePersistenceTests : IAsyncLifetime
{
    private readonly MetaMaskExtensionFixture _extension;
    private IPlaywright _playwright = null!;

    public CachePersistenceTests(MetaMaskExtensionFixture extension)
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
    // Scenario: Custom network added and selected → cache → reload
    //
    // Step 1 — fresh setup with a custom network added and selected as the active
    //           network; writes the profile to cache.
    // Step 2 — second service instance (same params → same cache key) hits the cache.
    //           No onboarding runs; only UnlockWalletAsync is called.
    //           The active network must still be the one selected in step 1.
    //
    // Note: WithNetworkToSelect is intentionally omitted from the step-2 service
    // because it is not part of the cache key — the network persists through the
    // cached Chromium profile, not through re-applying the setup steps.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CustomNetworkAddedAndSelected_CachedAndReloaded_NetworkPersists()
    {
        using var cache = new IsolatedCacheDirectory();

        var network = new NetworkConfig
        {
            Name       = "E2E BSC Testnet",
            RpcUrl     = "https://data-seed-prebsc-1-s1.binance.org:8545",
            ChainId    = "97",
            Symbol     = "tBNB"
        };

        // --- Step 1: fresh setup → add + select the custom network → write cache ---
        var warmupService = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithNetworkToAdd(network)
            .WithNetworkToSelect(network.Name)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true)
            .WithExtensionSaveDelayMs(500);

        var warmupContext = await warmupService.SetupAsync();
        await warmupService.CleanupAsync(warmupContext);

        Assert.Single(cache.CacheEntries());

        // --- Step 2: reload from cache (same cache key, no network steps re-run) ---
        var reloadService = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithNetworkToAdd(network)      // must match step-1 cache key
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(true);

        IBrowserContext? reloadContext = null;
        try
        {
            reloadContext = await reloadService.SetupAsync();

            await MetaMaskAssertions.AssertContextReadyAsync(reloadContext);

            var metaMaskPage = reloadContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
            await MetaMaskAssertions.AssertActiveNetworkAsync(metaMaskPage, network.Name);

            // Cache hit must not produce a second entry
            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (reloadContext != null)
                await reloadService.CleanupAsync(reloadContext);
        }
    }

    // -------------------------------------------------------------------------
    // Scenario: Additional imported account → cache → reload
    //
    // Step 1 — fresh setup with a private-key account import; MetaMask automatically
    //           switches to the imported account. Capture the active address, then
    //           write to cache.
    // Step 2 — second service instance (same params → same cache key) hits the cache.
    //           No onboarding runs; the same imported account must still be the active
    //           account (address shown in the header must match what was captured in
    //           step 1). This is more reliable than counting total accounts, which can
    //           vary across MetaMask versions or profiles.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AdditionalImportedAccount_CachedAndReloaded_AccountPersists()
    {
        using var cache = new IsolatedCacheDirectory();

        MetaMaskSetupService BuildService() =>
            new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
                .WithPassword(TestConfig.Password)
                .WithSeedPhrase(TestConfig.SeedPhrase)
                .WithAdditionalAccounts(TestConfig.PrivateKey)
                .WithContextCachePath(cache.Path)
                .UseContextCacheIfExists(true)
                .WithExtensionSaveDelayMs(500);

        // --- Step 1: fresh setup → import additional account → capture active address ---
        var warmupService = BuildService();
        var warmupContext = await warmupService.SetupAsync();
        var warmupPage = warmupContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
        // MetaMask switches to the imported account automatically; capture its address.
        var importedAccountAddress = await MetaMaskAssertions.GetActiveAccountAddressTextAsync(warmupPage);
        await warmupService.CleanupAsync(warmupContext);

        Assert.Single(cache.CacheEntries());

        // --- Step 2: reload from cache → verify the imported account is still active ---
        var reloadService = BuildService();
        IBrowserContext? reloadContext = null;
        try
        {
            reloadContext = await reloadService.SetupAsync();

            await MetaMaskAssertions.AssertContextReadyAsync(reloadContext);

            var metaMaskPage = reloadContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
            await MetaMaskAssertions.AssertActiveAccountAddressAsync(metaMaskPage, importedAccountAddress);

            // Cache hit must not produce a second entry
            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (reloadContext != null)
                await reloadService.CleanupAsync(reloadContext);
        }
    }

    // -------------------------------------------------------------------------
    // Scenario: Multiple settings together → cache → reload
    //
    // Step 1 — fresh setup with both a custom network AND an imported account; capture
    //           the active account address (the imported one, selected automatically).
    // Step 2 — reload from cache; both the active network AND the active account must
    //           match what was captured in step 1.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NetworkAndAdditionalAccount_CachedAndReloaded_BothSettingsPersist()
    {
        using var cache = new IsolatedCacheDirectory();

        var network = new NetworkConfig
        {
            Name    = "E2E BSC Testnet",
            RpcUrl  = "https://data-seed-prebsc-1-s1.binance.org:8545",
            ChainId = "97",
            Symbol  = "tBNB"
        };

        MetaMaskSetupService BuildService() =>
            new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
                .WithPassword(TestConfig.Password)
                .WithSeedPhrase(TestConfig.SeedPhrase)
                .WithNetworkToAdd(network)
                .WithNetworkToSelect(network.Name)
                .WithAdditionalAccounts(TestConfig.PrivateKey)
                .WithContextCachePath(cache.Path)
                .UseContextCacheIfExists(true)
                .WithExtensionSaveDelayMs(500);

        // --- Step 1: fresh setup with both settings → capture active account address ---
        var warmupService = BuildService();
        var warmupContext = await warmupService.SetupAsync();
        var warmupPage = warmupContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
        var importedAccountAddress = await MetaMaskAssertions.GetActiveAccountAddressTextAsync(warmupPage);
        await warmupService.CleanupAsync(warmupContext);

        Assert.Single(cache.CacheEntries());

        // --- Step 2: reload from cache; verify both settings survived ---
        var reloadService = BuildService();
        IBrowserContext? reloadContext = null;
        try
        {
            reloadContext = await reloadService.SetupAsync();

            await MetaMaskAssertions.AssertContextReadyAsync(reloadContext);

            var metaMaskPage = reloadContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
            await MetaMaskAssertions.AssertActiveNetworkAsync(metaMaskPage, network.Name);
            await MetaMaskAssertions.AssertActiveAccountAddressAsync(metaMaskPage, importedAccountAddress);

            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (reloadContext != null)
                await reloadService.CleanupAsync(reloadContext);
        }
    }
}
