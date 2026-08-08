namespace MetamaskSetup.Playwright.Tests.Helpers;

/// <summary>
/// Creates a temporary directory for use as an isolated MetaMask profile cache during a test,
/// then deletes it on dispose. Each test that deals with caching should use its own instance
/// to avoid cross-test contamination.
/// </summary>
public sealed class IsolatedCacheDirectory : IDisposable
{
    public string Path { get; }

    public IsolatedCacheDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "mm_test_cache_" + Guid.NewGuid().ToString("N")[..12]);

        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Returns all immediate subdirectories — each represents one cache entry written by the service.
    /// </summary>
    public string[] CacheEntries() =>
        Directory.GetDirectories(Path);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // best-effort; test should not fail in Dispose
        }
    }
}
