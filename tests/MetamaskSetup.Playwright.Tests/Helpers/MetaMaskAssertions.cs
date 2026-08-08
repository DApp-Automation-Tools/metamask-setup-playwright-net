using MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

namespace MetamaskSetup.Playwright.Tests.Helpers;

/// <summary>
/// Shared behavioral assertions for MetaMask wallet state.
/// All checks operate against live Playwright page state — no console output scraping.
/// </summary>
public static class MetaMaskAssertions
{
    /// <summary>
    /// Asserts that the MetaMask context is ready: non-null, has at least one page
    /// on a MetaMask extension URL, and the wallet is unlocked.
    /// The MetaMask page is located by URL, not by index, because the relaunch path
    /// (cache write → relaunch) may place it on a page other than index 0.
    /// </summary>
    public static async Task AssertContextReadyAsync(IBrowserContext context, int timeoutMs = 60_000)
    {
        Assert.NotNull(context);
        Assert.NotEmpty(context.Pages);

        var metaMaskPage = context.Pages.FirstOrDefault(p => p.Url.Contains("chrome-extension://"));
        Assert.NotNull(metaMaskPage);

        await AssertWalletUnlockedAsync(metaMaskPage, timeoutMs);
    }

    /// <summary>
    /// Asserts that the MetaMask home page is open and shows a valid 0x account address,
    /// confirming the wallet is unlocked and ready to use.
    /// </summary>
    public static async Task AssertWalletUnlockedAsync(IPage page, int timeoutMs = 60_000)
    {
        var addressButton = page.Locator(HomePageSelectors.CopyAccountAddressButton);
        await addressButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });

        var address = await addressButton.TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(address),
            "Expected a non-empty account address on the MetaMask home page.");
        Assert.StartsWith("0x", address!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asserts that the MetaMask onboarding page is visible, confirming no existing
    /// profile or cache was used (setup must run from scratch).
    /// </summary>
    public static async Task AssertOnboardingPageAsync(IPage page, int timeoutMs = 10_000)
    {
        // Either button is enough to confirm we are on the onboarding flow.
        var createButton = page.Locator(OnboardingPageSelectors.CreateNewWallet);
        var importButton = page.Locator(OnboardingPageSelectors.ImportWallet);

        var createTask = createButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        var importTask = importButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });

        var completed = await Task.WhenAny(createTask, importTask);
        await completed; // re-throw if both timed out
    }

    /// <summary>
    /// Asserts that the network currently selected in MetaMask matches
    /// <paramref name="expectedNetworkName"/>. Use this after loading from cache to
    /// verify that a network addition or switch persisted correctly.
    /// </summary>
    public static async Task AssertActiveNetworkAsync(IPage page, string expectedNetworkName, int timeoutMs = 10_000)
    {
        var networkLocator = page.Locator(HomePageSelectors.NetworkDropdown.CurrentNetwork);
        await networkLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });

        var currentNetwork = (await networkLocator.InnerTextAsync())?.Trim();
        Assert.Equal(expectedNetworkName, currentNetwork);
    }

    /// <summary>
    /// Returns the text content of the active-account address button shown in the MetaMask
    /// header (may be a truncated form such as "0x2c75…c23"). Use in combination with
    /// <see cref="AssertActiveAccountAddressAsync"/> to verify account selection persistence
    /// across a cache round-trip without hard-coding addresses in tests.
    /// </summary>
    public static async Task<string> GetActiveAccountAddressTextAsync(IPage page, int timeoutMs = 10_000)
    {
        var addressButton = page.Locator(HomePageSelectors.CopyAccountAddressButton);
        await addressButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        return (await addressButton.TextContentAsync())?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Asserts that the active account shown in the MetaMask header matches
    /// <paramref name="expectedAddressText"/> (the value previously returned by
    /// <see cref="GetActiveAccountAddressTextAsync"/>). Use this after loading from cache to
    /// verify that the correct account was restored as the active account.
    /// </summary>
    public static async Task AssertActiveAccountAddressAsync(IPage page, string expectedAddressText, int timeoutMs = 10_000)
    {
        var actual = await GetActiveAccountAddressTextAsync(page, timeoutMs);
        Assert.Equal(expectedAddressText, actual);
    }
}
