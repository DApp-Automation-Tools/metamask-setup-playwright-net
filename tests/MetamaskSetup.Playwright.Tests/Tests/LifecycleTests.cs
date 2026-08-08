namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Covers:
///   Scenario: Normal teardown
///   Scenario: Teardown after a setup failure
///   Scenario: Calling CleanupAsync multiple times
/// </summary>
[Collection(MetaMaskTestCollection.Name)]
public sealed class LifecycleTests : IAsyncLifetime
{
    private readonly MetaMaskExtensionFixture _extension;
    private IPlaywright _playwright = null!;

    public LifecycleTests(MetaMaskExtensionFixture extension)
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
    public async Task CleanupAsync_NormalTeardown_ClosesContextAndDeletesTempProfile()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        var snapshot = new TempProfileSnapshot();
        var result = await service.SetupAsync();

        var exception = await Record.ExceptionAsync(() => service.CleanupAsync(result));

        Assert.Null(exception);
        snapshot.AssertAllNewDirectoriesDeleted();
    }

    [Fact]
    public async Task CleanupAsync_WithNullContext_DoesNotThrow()
    {
        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password);

        var exception = await Record.ExceptionAsync(() => service.CleanupAsync((IBrowserContext)null!));
        Assert.Null(exception);
    }

    [Fact]
    public async Task CleanupAsync_CalledTwice_IsIdempotent()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        var result = await service.SetupAsync();

        await service.CleanupAsync(result);

        var exception = await Record.ExceptionAsync(() => service.CleanupAsync(result.Context));
        Assert.Null(exception);
    }
}
