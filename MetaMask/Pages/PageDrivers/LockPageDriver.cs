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
            return;
        }

        await page.Locator(LockPageSelectors.PasswordInput).FillAsync(password);
        await page.Locator(LockPageSelectors.SubmitButton).ClickAsync();
        await MetaMaskUtils.WaitForSpinnerToVanishAsync(page);
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
}