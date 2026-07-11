using MetaMaskDownloadManager;
using MetaMaskDownloadManager.Meta;
using MetaMaskDownloadManager.Models.Common;

namespace MetamaskSetup.Playwright.Tests.Fixtures;

/// <summary>
/// xUnit collection fixture that ensures the MetaMask extension is available exactly once
/// for the entire test run.
///
/// Resolution order:
///   1. METAMASK_EXTENSION_PATH env var — use as-is (fast local dev / pre-cached CI).
///   2. Otherwise — download MetaMask <see cref="MetamaskSetup.Playwright.Meta.Constants.METAMASK_VERSION"/>
///      from GitHub via MetaMaskDownloadManager into a temporary directory.
///
/// Set GITHUB_TOKEN to avoid GitHub API rate limits in CI.
/// The temporary download directory is deleted when the fixture is disposed (end of test run).
/// </summary>
public sealed class MetaMaskExtensionFixture : IDisposable
{
    private readonly string? _downloadTempDir;

    /// <summary>
    /// Full path to the unpacked MetaMask extension directory (the folder containing manifest.json).
    /// Pass directly to Playwright's --load-extension flag.
    /// </summary>
    public string ExtensionPath { get; }

    public MetaMaskExtensionFixture()
    {
        var envPath = Environment.GetEnvironmentVariable("METAMASK_EXTENSION_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            ExtensionPath = envPath;
            Console.WriteLine($"[MetaMaskExtensionFixture] Using extension from METAMASK_EXTENSION_PATH: {ExtensionPath}");
            return;
        }

        _downloadTempDir = Path.Combine(
            Path.GetTempPath(),
            "mm_ext_" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_downloadTempDir);

        var pinnedVersion = MetamaskSetup.Playwright.Meta.Constants.METAMASK_VERSION;
        Console.WriteLine($"[MetaMaskExtensionFixture] Downloading MetaMask {pinnedVersion} to {_downloadTempDir} ...");

        var service = new MetaMaskDownloadManagerService();
        ExtensionPath = service.DownloadMetaMask(new DownloadManagerOptions
        {
            BrowserType = CustomBrowserType.Chrome,
            Version = pinnedVersion,
            DownloadPath = _downloadTempDir
        });

        Console.WriteLine($"[MetaMaskExtensionFixture] Extension ready at: {ExtensionPath}");
    }

    public void Dispose()
    {
        if (_downloadTempDir != null)
        {
            try { Directory.Delete(_downloadTempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
