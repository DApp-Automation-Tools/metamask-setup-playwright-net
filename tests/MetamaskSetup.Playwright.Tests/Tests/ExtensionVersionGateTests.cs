using MetamaskSetup.Playwright.Meta;
using MetamaskSetup.Playwright.Services;

namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Version-gate tests — no MetaMask UI required.
/// </summary>
public sealed class ExtensionVersionGateTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private string _tempExtensionDir = null!;

    public async Task InitializeAsync()
    {
        _playwright = await PlaywrightFactory.CreateAsync();
        _tempExtensionDir = Path.Combine(Path.GetTempPath(), "mm_version_gate_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempExtensionDir);
    }

    public Task DisposeAsync()
    {
        _playwright.Dispose();
        try
        {
            if (Directory.Exists(_tempExtensionDir))
                Directory.Delete(_tempExtensionDir, recursive: true);
        }
        catch
        {
            // best-effort
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SetupAsync_MismatchedManifestVersion_ThrowsBeforeLaunch()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempExtensionDir, "manifest.json"),
            """{"version":"0.0.0","manifest_version":3,"name":"fake"}""");

        var service = new MetaMaskSetupService(_playwright.Chromium, _tempExtensionDir)
            .WithPassword("irrelevant");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetupAsync());
        Assert.Contains(Constants.METAMASK_VERSION, ex.Message);
        Assert.Contains("0.0.0", ex.Message);
    }

    [Fact]
    public async Task SetupAsync_MissingManifest_ThrowsClearError()
    {
        var service = new MetaMaskSetupService(_playwright.Chromium, _tempExtensionDir)
            .WithPassword("irrelevant");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetupAsync());
        Assert.Contains("manifest.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
