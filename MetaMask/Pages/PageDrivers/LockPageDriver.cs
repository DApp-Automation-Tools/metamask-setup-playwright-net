using MetamaskSetup.Playwright.MetaMask.Pages.Selectors;
using MetamaskSetup.Playwright.Utils;
using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.MetaMask.Pages.PageDrivers;

public class LockPageDriver
{
    public async Task UnlockAsync(IPage page, string password)
    {
        try
        {
            await WaitForLockedAsync(page, 15000);
        }
        catch (TimeoutException)
        {
            await EnsureHomeUiReachableAsync(page);
            return;
        }

        await page.Locator(LockPageSelectors.PasswordInput).FillAsync(password);
        await page.Locator(LockPageSelectors.SubmitButton).ClickAsync();
        await MetaMaskUtils.WaitForSpinnerToVanishAsync(page);
        await EnsureHomeUiReachableAsync(page, timeoutMs: 15_000);
    }

    public async Task<bool> IsLocked(IPage page)
    {
        try
        {
            await page.Locator(LockPageSelectors.PasswordInput)
                .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task WaitForLockedAsync(IPage page, int timeoutMs = 15000)
    {
        await page.Locator(LockPageSelectors.PasswordInput)
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
    }
    private static async Task EnsureHomeUiReachableAsync(IPage page, int timeoutMs = 5_000)
    {
        var addressButton = page.Locator(HomePageSelectors.CopyAccountAddressButton);
        try
        {
            await addressButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                "MetaMask did not show the lock screen or an unlocked home UI within the timeout. " +
                "The wallet may have failed to load, or the wrong page was selected.");
        }
    }
}
