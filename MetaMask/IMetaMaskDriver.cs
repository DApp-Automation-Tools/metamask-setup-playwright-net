using MetamaskSetup.Playwright.Models;

namespace MetamaskSetup.Playwright.MetaMask;

public interface IMetaMaskDriver
{
    Task AddNetworkAsync(NetworkConfig networkConfig);
    Task CreateNewWalletAsync(string password);
    Task ImportWalletAsync(string seedPhrase);
    Task ImportWalletFromPrivateKeyAsync(string privateKey);
    Task LockWalletAsync();
    Task SwitchAccountByAddressAsync(string accountAddress);
    Task SwitchAccountByNameAsync(string accountName);
    Task SwitchNetworkAsync(string networkName);
    Task UnlockWalletAsync();
    Task GetCurrentNetworkNameAsync();
}