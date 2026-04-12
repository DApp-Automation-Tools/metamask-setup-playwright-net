using MetamaskSetup.Playwright.MetaMask.Pages.Selectors;
using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.Utils
{
    public static class MetaMaskUtils
    {
        private const int DEFAULT_TIMEOUT = 10000;

         public static async Task<IPage> WaitForMetaMaskLoadAsync(IPage page)
        {
            try
            {
                await WaitUtils.WaitUntilStableAsync(page);

                var loadingIndicators = LoadingSelectors.LoadingIndicators;
                var tasks = new List<Task>();
                foreach (var selector in loadingIndicators)
                {
                    tasks.Add(WaitUtils.WaitForSelectorAsync(selector, page, DEFAULT_TIMEOUT));
                }
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning during MetaMask load: {ex}");
            }

            await WaitUtils.SleepAsync(300);
            return page;
        }

        public static async Task WaitForMetaMaskWindowToBeStableAsync(IPage page)
        {
            await WaitForMetaMaskLoadAsync(page);
            if (await page.Locator(ErrorSelectors.LoadingOverlayErrorButtons).CountAsync() > 0)
            {
                var retryButton = page.Locator(ErrorSelectors.LoadingOverlayErrorButtonsRetryButton);
                await retryButton.ClickAsync();
                await WaitUtils.WaitForSelectorAsync(LoadingSelectors.LoadingOverlay, page, DEFAULT_TIMEOUT);
            }
            await FixCriticalErrorAsync(page);
        }

        public static async Task FixCriticalErrorAsync(IPage page, int maxRetries = 5)
        {
            for (int times = 0; times < maxRetries; times++)
            {
                if (await page.Locator(ErrorSelectors.CriticalError).CountAsync() > 0)
                {
                    Console.WriteLine($"[fixCriticalError] Metamask crashed with critical error, refreshing.. (attempt {times + 1})");

                    if (times <= 3)
                    {
                        await page.ReloadAsync();
                        await WaitForMetaMaskWindowToBeStableAsync(page);
                    }
                    else if (times == 4)
                    {
                        var restartButton = page.Locator(ErrorSelectors.CriticalErrorRestartButton);
                        await restartButton.ClickAsync();
                        await WaitForMetaMaskWindowToBeStableAsync(page);
                    }
                    else
                    {
                        throw new Exception("[fixCriticalError] Max amount of retries to fix critical metamask error has been reached.");
                    }
                }
                else if (await page.Locator(ErrorSelectors.ErrorPage).CountAsync() > 0)
                {
                    Console.WriteLine($"[fixCriticalError] Metamask crashed with error, refreshing.. (attempt {times + 1})");

                    if (times <= 4)
                    {
                        await page.ReloadAsync();
                        await WaitForMetaMaskWindowToBeStableAsync(page);
                    }
                    else
                    {
                        throw new Exception("[fixCriticalError] Max amount of retries to fix critical metamask error has been reached.");
                    }
                }
                else
                {
                    break;
                }

                await WaitUtils.SleepAsync(500 * (times + 1));
            }
        }

        public static async Task WaitForSpinnerToVanishAsync(IPage page)
        {
            await page.Locator(LoadingSelectors.Spinner).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = DEFAULT_TIMEOUT
            });
        }
    }
}