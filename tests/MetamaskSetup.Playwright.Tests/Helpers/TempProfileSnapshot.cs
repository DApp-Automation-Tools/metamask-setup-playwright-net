namespace MetamaskSetup.Playwright.Tests.Helpers;

/// <summary>
/// Snapshots the set of MetaMask temp profile directories in %TEMP% at construction time,
/// then lets tests discover which new directories were created afterward and whether they
/// have been cleaned up.
///
/// MetaMaskSetupService creates temp profiles at:
///   %TEMP%\playwright_metamask_{guid}
/// </summary>
public sealed class TempProfileSnapshot
{
    private readonly HashSet<string> _baseline;

    public TempProfileSnapshot()
    {
        _baseline = Snapshot();
    }

    /// <summary>
    /// Returns directories matching playwright_metamask_* that exist in %TEMP% now
    /// but were not present when this snapshot was taken.
    /// </summary>
    public IReadOnlyList<string> NewDirectories()
    {
        return Snapshot()
            .Except(_baseline, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Asserts that all directories created since the snapshot was taken have since been deleted.
    /// Useful in teardown tests.
    /// </summary>
    public void AssertAllNewDirectoriesDeleted()
    {
        var surviving = NewDirectories();
        if (surviving.Count == 0)
            return;

        throw new Xunit.Sdk.XunitException(
            $"Expected temp MetaMask profile directories to be deleted, but {surviving.Count} still exist:\n" +
            string.Join("\n", surviving));
    }

    /// <summary>
    /// Returns true if at least one new temp profile was created since the snapshot.
    /// </summary>
    public bool AnyNewDirectoriesExist() => NewDirectories().Count > 0;

    private static HashSet<string> Snapshot()
    {
        var tempPath = Path.GetTempPath();
        return Directory.GetDirectories(tempPath, "playwright_metamask_*")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
