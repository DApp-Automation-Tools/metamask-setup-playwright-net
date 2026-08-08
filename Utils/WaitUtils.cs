using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.Utils
{
    public static class WaitUtils
    {
        private const int DEFAULT_TIMEOUT = 10000;
        private static readonly int[] Timeouts = [0, 20, 50, 100, 100, 500];

        public static async Task WaitUntilStableAsync(IPage page)
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = DEFAULT_TIMEOUT });
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = DEFAULT_TIMEOUT });
            }
            catch (TimeoutException)
            {
                // Extensions often keep RPC/WebSocket traffic alive; do not fail setup on NetworkIdle.
                Console.WriteLine("[WaitUtils] NetworkIdle timed out — continuing.");
            }
        }

        public static async Task WaitForSelectorAsync(string selector, IPage page, int timeout)
        {
            await WaitUntilStableAsync(page);

            try
            {
                await page.WaitForSelectorAsync(selector, new() { State = WaitForSelectorState.Hidden, Timeout = timeout });
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Loading indicator `{selector}` not found - continuing.");
            }
            catch (Exception)
            {
                Console.WriteLine($"Error while waiting for loading indicator `{selector}` to disappear");
                throw;
            }
        }

       
        public static async Task<bool> WaitForAsync(Func<Task<bool>> action, int timeout, bool shouldThrow = true)
        {
            int timeoutsSum = 0;
            int timeoutIndex = 0;
            bool reachedTimeout = false;

            while (!reachedTimeout)
            {
                int nextTimeout = Timeouts[Math.Min(timeoutIndex++, Timeouts.Length - 1)];

                if (timeoutsSum + nextTimeout > timeout)
                {
                    nextTimeout = timeout - timeoutsSum;
                    reachedTimeout = true;
                }
                else
                {
                    timeoutsSum += nextTimeout;
                }

                await SleepAsync(nextTimeout);

                if (await action())
                {
                    return true;
                }
            }

            if (shouldThrow)
            {
                throw new TimeoutException($"Timeout {timeout}ms exceeded.");
            }

            return false;
        }

        public static Task SleepAsync(int ms) => Task.Delay(ms);
    }
}