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

    // -------------------------------------------------------------------------
    // Scenario: Normal teardown
    //
    // CleanupAsync closes the browser context without throwing.
    // After cleanup the temp profile directory is removed.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_NormalTeardown_ClosesContextAndDeletesTempProfile()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        var snapshot = new TempProfileSnapshot();
        var context = await service.SetupAsync();

        var exception = await Record.ExceptionAsync(() => service.CleanupAsync(context));

        Assert.Null(exception);
        snapshot.AssertAllNewDirectoriesDeleted();
    }

    // -------------------------------------------------------------------------
    // Scenario: Teardown after a setup failure
    //
    // When SetupAsync never completes successfully, CleanupAsync with null is safe.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_WithNullContext_DoesNotThrow()
    {
        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password);

        var exception = await Record.ExceptionAsync(() => service.CleanupAsync(null!));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------------
    // Scenario: Calling CleanupAsync multiple times
    //
    // Idempotent — a second call on an already-closed context must not throw.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_CalledTwice_IsIdempotent()
    {
        using var cache = new IsolatedCacheDirectory();

        var service = new MetaMaskSetupService(_playwright.Chromium, _extension.ExtensionPath)
            .WithPassword(TestConfig.Password)
            .WithContextCachePath(cache.Path)
            .UseContextCacheIfExists(false);

        var context = await service.SetupAsync();

        await service.CleanupAsync(context);

        var exception = await Record.ExceptionAsync(() => service.CleanupAsync(context));
        Assert.Null(exception);
    }
}
