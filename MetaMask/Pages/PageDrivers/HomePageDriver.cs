using MetamaskSetup.Playwright.MetaMask.Pages.Selectors;
using MetamaskSetup.Playwright.Models;
using MetamaskSetup.Playwright.Utils;
using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.MetaMask.Pages.PageDrivers;

public class HomePageDriver
{
    public async Task<string> GetCurrentNetworkNameAsync(IPage page)
    {
        return (await page.Locator(HomePageSelectors.NetworkDropdown.CurrentNetwork).InnerTextAsync()).Trim();
    }
    
    public async Task AddNetworkAsync(IPage page, NetworkConfig networkConfig)
    {
        await page.Locator(HomePageSelectors.NetworkDropdown.NetworksDropdownButton).ClickAsync();
        await page.Locator(SettingsPageSelectors.CustomNetworksTabButton).ClickAsync();
        await page.Locator(SettingsPageSelectors.AddCustomNetworkButton).ClickAsync();
        
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.NetworkNameInput).FillAsync(networkConfig.Name);
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.AddRpcDropdown).ClickAsync();
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.AddRpcButton).ClickAsync();
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.RpcUrlInput).FillAsync(networkConfig.RpcUrl);
        
        var rpcUrlErrorLocator = page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.RpcUrlError);
        
        await CheckForRpcUrlErrorAsync(rpcUrlErrorLocator);
        
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.RpcNameInput).ClickAsync();
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.AddRpcUrlButton).ClickAsync();

        await CheckForRpcUrlErrorAsync(rpcUrlErrorLocator);
        
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.ChainIdInput).FillAsync(networkConfig.ChainId);

        await CheckForRpcUrlErrorAsync(rpcUrlErrorLocator);
        
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.SymbolInput).FillAsync(networkConfig.Symbol);
        
        if (!string.IsNullOrEmpty(networkConfig.BlockExplorerUrl))
        {
            await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.BlockExplorerDropdown).ClickAsync();
            await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.AddBlockExplorerButton).ClickAsync();
            await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.BlockExplorerUrlInput)
                .FillAsync(networkConfig.BlockExplorerUrl);
            await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.AddBlockExplorerUrlButton).ClickAsync();
        }
        
        await page.Locator(SettingsPageSelectors.Networks.NewNetworkForm.SaveButton).ClickAsync();

        await page.Locator(SettingsPageSelectors.Networks.CloseModalButton).ClickAsync();
        
        await page.Locator(HomePageSelectors.NetworkDropdown.CurrentNetwork).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await WaitUtils.WaitForAsync(
            async () => (await page.Locator(HomePageSelectors.NetworkDropdown.CurrentNetwork).InnerTextAsync())?.Trim() == networkConfig.Name,
            20_000
        );

        await CloseNewNetworkInfoPopoverAsync(page);
        await ClosePopoverAsync(page);
    }
    
    public async Task ImportWalletFromPrivateKeyAsync(IPage page, string privateKey)
    {
        await page.Locator(HomePageSelectors.AccountMenu.AccountButton).ClickAsync();
        await page.Locator(HomePageSelectors.AccountMenu.AddAccountOrWalletButton).ClickAsync();
        await page.Locator(HomePageSelectors.AccountMenu.ImportWithPrivateKeyButton).ClickAsync();
        
        await page.Locator(HomePageSelectors.AccountMenu.PrivateKeyInput).FillAsync(privateKey);
        
        var importButton = page.Locator(HomePageSelectors.AccountMenu.ConfirmImportButton);
        await importButton.ClickAsync();
    
        var isImportButtonHidden = await WaitUtils.WaitForAsync(
            async () => await importButton.IsHiddenAsync(),
            10_000,
            false
        );
    
        if (!isImportButtonHidden)
        {
            var errorText = await page.Locator(HomePageSelectors.AccountMenu.ImportAccountError)
                .TextContentAsync(new() { Timeout = 1_000 });
        
            throw new Exception($"[ImportWalletFromPrivateKey] Importing failed due to error: {errorText}");
        }
    }
    
    public async Task SwitchAccountByNameAsync(IPage page, string accountName)
    {
        await page.Locator(HomePageSelectors.AccountMenu.AccountButton).ClickAsync();
    
         var accountNamesLocators = await page.Locator(HomePageSelectors.AccountMenu.AccountNames).AllAsync();
         var accountNames = new List<string>();
        
        foreach (var locator in accountNamesLocators)
        {
            var text = await locator.InnerTextAsync();
            accountNames.Add(text?.Trim() ?? string.Empty);
        }
        
        var lowerAccountName = accountName.ToLowerInvariant();
        var accountIndex = accountNames.FindIndex(name => name.ToLowerInvariant() == lowerAccountName);
        
        if (accountIndex == -1)
            throw new Exception($"[SwitchAccount] Account with name {accountName} not found");
        
        await accountNamesLocators[accountIndex].ClickAsync();
    }
    
    public async Task LockWalletAsync(IPage page)
    {
        await page.Locator(HomePageSelectors.AccountOptionsMenuButton).ClickAsync();
        await page.Locator(HomePageSelectors.GlobalMenuLockButton).ClickAsync();
        await WaitUtils.SleepAsync(500);
    }

    public async Task SwitchAccountByAddressAsync(IPage page, string accountAddress)
    {
        await page.Locator(HomePageSelectors.AccountMenu.AccountButton).ClickAsync();
        await page.Locator(HomePageSelectors.AccountMenu.AccountNames).First
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await page.Locator(HomePageSelectors.AccountMenu.SearchBarInput).FillAsync(accountAddress);

        var accountsLocators = await page.Locator(HomePageSelectors.AccountMenu.AccountNames).AllAsync();

        if (accountsLocators.Count == 0)
            throw new Exception("[SwitchToFirstAccount] No accounts found");

        await accountsLocators[0].ClickAsync();
    }
    
    public async Task SwitchNetworkAsync(IPage page, string networkName)
    {
        await page.Locator(HomePageSelectors.NetworkDropdown.NetworksDropdownButton).ClickAsync();

        if (await SwitchNetworkFromPopularAsync(page, networkName)) return;
        if (await SwitchNetworkFromAdditionalAsync(page, networkName)) return;
        if (await SwitchNetworkFromCustomAsync(page, networkName)) return;
    
        throw new Exception($"[SwitchNetwork] Network with name {networkName} not found");
    }
    
    private static async Task WaitForNetworkSwitchAsync(IPage page, string expectedNetworkName)
    {
        await page.Locator(HomePageSelectors.NetworkDropdown.CurrentNetwork)
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await WaitUtils.WaitForAsync(
            async () => (await page.Locator(HomePageSelectors.NetworkDropdown.CurrentNetwork).InnerTextAsync())?.Trim() == expectedNetworkName,
            10_000
        );
    }
    private async Task<bool> SwitchNetworkFromPopularAsync(IPage page, string networkName)
    {
        await page.Locator(SettingsPageSelectors.PopularNetworksTabButton).ClickAsync();

        var locator = page.Locator(SettingsPageSelectors.NetworksItem(networkName));
        if (await locator.IsVisibleAsync())
        {
            await locator.ClickAsync();
            await WaitForNetworkSwitchAsync(page, networkName);
            return true;
        }
        return false;
    }
    
    private async Task<bool> SwitchNetworkFromAdditionalAsync(IPage page, string networkName)
    {
        await page.Locator(SettingsPageSelectors.PopularNetworksTabButton).ClickAsync();
    
        var found = await ClickNetworkByNameAsync(page, SettingsPageSelectors.AdditionalNetworkItem, networkName);
        if (found)
        {
            await HandleConfirmAddingNewNetworkAsync(page);
            await WaitForNetworkSwitchAsync(page, networkName);
            return true;
        }
        return false;
    }
    
    private async Task<bool> SwitchNetworkFromCustomAsync(IPage page, string networkName)
    {
        await page.Locator(SettingsPageSelectors.CustomNetworksTabButton).ClickAsync();

        var locator = page.Locator(SettingsPageSelectors.NetworksItem(networkName));
        if (await locator.IsVisibleAsync())
        {
            await locator.ClickAsync();
            await WaitForNetworkSwitchAsync(page, networkName);
            return true;
        }
        return false;
    }
    
    private static async Task HandleConfirmAddingNewNetworkAsync(IPage page)
    {
        var confirmButton = page.Locator(SettingsPageSelectors.ConfirmAddingNewNetwork);
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        if (await confirmButton.IsVisibleAsync())
        {
            await confirmButton.ClickAsync();
        }
    }

    private static async Task<bool> ClickNetworkByNameAsync(IPage page, string itemSelector, string networkName)
    {
        var items = await page.Locator(itemSelector).AllAsync();
        foreach (var item in items)
        {
            var text = (await item.InnerTextAsync())?.Trim();
            if (string.Equals(text, networkName, StringComparison.OrdinalIgnoreCase))
            {
                await item.ClickAsync();
                return true;
            }
        }
        return false;
    }
    
    public async Task CloseNewNetworkInfoPopoverAsync(IPage page)
    {
        var gotItButtonLocator = page.Locator(HomePageSelectors.NewNetworkInfoPopover.GotItButton).First;

        await ClickLocatorIfConditionAsync(
            gotItButtonLocator,
            async () => await gotItButtonLocator.IsVisibleAsync(),
            1_000
        );
    }

    public async Task CloseWhatsNewPopoverAsync(IPage page)
    {
        var closeButtonLocator = page.Locator("[aria-label='Close']").Last;

        await ClickLocatorIfConditionAsync(
            closeButtonLocator,
            async () => await closeButtonLocator.IsVisibleAsync(),
            1_000
        );
    }
    
    public static async Task ClickLocatorIfConditionAsync(ILocator locator, Func<Task<bool>> condition, int timeout)
    {
        if (await WaitUtils.WaitForAsync(condition, timeout, false))
        {
            await locator.ClickAsync();
        }
    }

    public async Task ClosePopoverAsync(IPage page)
    {
        var closeButtonLocator = page.Locator(HomePageSelectors.Popover.CloseButton).First;

        await ClickLocatorIfConditionAsync(
            closeButtonLocator,
            async () => await closeButtonLocator.IsVisibleAsync(),
            1_000
        );
    }
    
    private static async Task CheckForRpcUrlErrorAsync(ILocator rpcUrlErrorLocator)
    {
        if (await WaitUtils.WaitForAsync(
                async () => await rpcUrlErrorLocator.IsVisibleAsync(),
                1_000,
                false))
        {
            var rpcUrlErrorText = await rpcUrlErrorLocator.TextContentAsync(new() { Timeout = 1_000 }) ?? "Unknown RPC URL Error";
            throw new Exception($"[AddNetwork] RPC URL validation failed: {rpcUrlErrorText}");
        }
    }
}