namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Covers:
///   Scenario: Import with seed phrase and optional network
/// </summary>
[Collection(MetaMaskTestCollection.Name)]
public sealed class WalletImportTests : IAsyncLifetime
{
    private readonly MetaMaskExtensionFixture _extension;
    private IPlaywright _playwright = null!;

    public WalletImportTests(MetaMaskExtensionFixture extension)
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
    [Trait("Category", "Smoke")]
    public async Task ImportWallet_ValidSeedPhrase_ReturnsUsableContext()
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
            Assert.False(string.IsNullOrWhiteSpace(result.ExtensionId));
            await MetaMaskAssertions.AssertContextReadyAsync(context);
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ImportWallet_WithCustomNetwork_NetworkAddedAfterOnboarding()
    {
        using var cache = new IsolatedCacheDirectory();

        var network = new NetworkConfig
        {
            Name = "E2E BSC Testnet",
            RpcUrl = "https://data-seed-prebsc-1-s1.binance.org:8545",
            ChainId = "97",
            Symbol = "tBNB"
        };

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithSeedPhrase(TestConfig.SeedPhrase)
            .WithNetworkToAdd(network)
            .WithNetworkToSelect("E2E BSC Testnet")
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        IBrowserContext? context = null;
        try
        {
            var result = await service.SetupAsync();
            context = result.Context;
            await MetaMaskAssertions.AssertContextReadyAsync(context);
        }
        finally
        {
            if (context != null)
                await service.CleanupAsync(context);
        }
    }
}
