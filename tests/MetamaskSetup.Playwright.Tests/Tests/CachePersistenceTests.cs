namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Verifies that MetaMask settings survive the full cache round-trip.
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

    [Fact]
    public async Task CustomNetworkAddedAndSelected_CachedAndReloaded_NetworkPersists()
    {
        using var cache = new IsolatedCacheDirectory();

        var network = new NetworkConfig
        {
            Name = "E2E BSC Testnet",
            RpcUrl = "https://data-seed-prebsc-1-s1.binance.org:8545",
            ChainId = "97",
            Symbol = "tBNB"
        };

        MetaMaskSetupService BuildService() =>
            new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
                .WithPassword(TestConfig.Password)
                .WithSeedPhrase(TestConfig.SeedPhrase)
                .WithNetworkToAdd(network)
                .WithNetworkToSelect(network.Name)
                .WithContextCachePath(cache.Path)
                .UseContextCacheIfExists(true)
                .WithExtensionSaveDelayMs(500);

        var warmupService = BuildService();
        var warmupResult = await warmupService.SetupAsync();
        await warmupService.CleanupAsync(warmupResult);

        Assert.Single(cache.CacheEntries());

        var reloadService = BuildService();
        IBrowserContext? reloadContext = null;
        try
        {
            var reloadResult = await reloadService.SetupAsync();
            reloadContext = reloadResult.Context;

            await MetaMaskAssertions.AssertContextReadyAsync(reloadContext);

            var metaMaskPage = reloadContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
            await MetaMaskAssertions.AssertActiveNetworkAsync(metaMaskPage, network.Name);

            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (reloadContext != null)
                await reloadService.CleanupAsync(reloadContext);
        }
    }

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

        var warmupService = BuildService();
        var warmupResult = await warmupService.SetupAsync();
        var warmupPage = warmupResult.Context.Pages.First(p => p.Url.Contains("chrome-extension://"));
        var importedAccountAddress = await MetaMaskAssertions.GetActiveAccountAddressTextAsync(warmupPage);
        await warmupService.CleanupAsync(warmupResult);

        Assert.Single(cache.CacheEntries());

        var reloadService = BuildService();
        IBrowserContext? reloadContext = null;
        try
        {
            var reloadResult = await reloadService.SetupAsync();
            reloadContext = reloadResult.Context;

            await MetaMaskAssertions.AssertContextReadyAsync(reloadContext);

            var metaMaskPage = reloadContext.Pages.First(p => p.Url.Contains("chrome-extension://"));
            await MetaMaskAssertions.AssertActiveAccountAddressAsync(metaMaskPage, importedAccountAddress);

            Assert.Single(cache.CacheEntries());
        }
        finally
        {
            if (reloadContext != null)
                await reloadService.CleanupAsync(reloadContext);
        }
    }

    [Fact]
    public async Task NetworkAndAdditionalAccount_CachedAndReloaded_BothSettingsPersist()
    {
        using var cache = new IsolatedCacheDirectory();

        var network = new NetworkConfig
        {
            Name = "E2E BSC Testnet",
            RpcUrl = "https://data-seed-prebsc-1-s1.binance.org:8545",
            ChainId = "97",
            Symbol = "tBNB"
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

        var warmupService = BuildService();
        var warmupResult = await warmupService.SetupAsync();
        var warmupPage = warmupResult.Context.Pages.First(p => p.Url.Contains("chrome-extension://"));
        var importedAccountAddress = await MetaMaskAssertions.GetActiveAccountAddressTextAsync(warmupPage);
        await warmupService.CleanupAsync(warmupResult);

        Assert.Single(cache.CacheEntries());

        var reloadService = BuildService();
        IBrowserContext? reloadContext = null;
        try
        {
            var reloadResult = await reloadService.SetupAsync();
            reloadContext = reloadResult.Context;

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
