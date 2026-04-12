using MetamaskSetup.Playwright.MetaMask.Pages.PageDrivers;
using MetamaskSetup.Playwright.Models;
using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.MetaMask
{
    public class MetaMaskDriver(IBrowserContext context, IPage page, string password, string? extensionId)
        : IMetaMaskDriver
    {
        private OnboardingPageDriver OnboardingPage { get; } = new();
        private HomePageDriver HomePage { get; } = new();
        private LockPageDriver LockPage { get; } = new();

        public async Task ImportWalletAsync(string seedPhrase)
        {
            await OnboardingPage.ImportWalletAsync(page, seedPhrase, password);
        }

        public async Task ImportWalletFromPrivateKeyAsync(string privateKey)
        {
            await HomePage.ImportWalletFromPrivateKeyAsync(page, privateKey);
        }
        
        public async Task AddNetworkAsync(NetworkConfig networkConfig)
        {
            await HomePage.AddNetworkAsync(page, networkConfig);
        }
        
        public async Task SwitchAccountByNameAsync(string accountName)
        {
            await HomePage.SwitchAccountByNameAsync(page, accountName);
        }
        
        public async Task SwitchAccountByAddressAsync(string accountAddress)
        {
            await HomePage.SwitchAccountByAddressAsync(page, accountAddress);
        }
        
        public async Task SwitchNetworkAsync(string networkName)
        {
            await HomePage.SwitchNetworkAsync(page, networkName);
        }
        
        public async Task CreateNewWalletAsync(string password)
        {
            await OnboardingPage.CreateNewWalletAsync(page, password);
        }
        
        public async Task GetCurrentNetworkNameAsync()
        {
            await HomePage.GetCurrentNetworkNameAsync(page);
        }
        
        public async Task LockWalletAsync()
        {
            await HomePage.LockWalletAsync(page);
            await LockPage.WaitForLockedAsync(page);
        }

        public async Task UnlockWalletAsync()
        {
            await LockPage.UnlockAsync(page, password);
        }
    }
}