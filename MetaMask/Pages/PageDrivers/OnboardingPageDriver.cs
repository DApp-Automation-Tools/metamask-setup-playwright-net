using MetamaskSetup.Playwright.MetaMask.Pages.Selectors;
using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.MetaMask.Pages.PageDrivers;

public class OnboardingPageDriver
{
    public async Task CreateNewWalletAsync(IPage page, string password)
    {
        await page.Locator(OnboardingPageSelectors.GetStartedButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.TermsOfUseCheckbox).ClickAsync();
        await page.Locator(OnboardingPageSelectors.TermsOfUseScrollButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.TermsOfUseAgreeButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.CreateNewWallet).ClickAsync();
        await page.Locator(OnboardingPageSelectors.CreateUsingSrpButton).ClickAsync();
        
        await CreatePasswordAsync(page, password);
 
        await page.Locator(OnboardingPageSelectors.SecureWalletLaterButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.SrpBackupCheckbox).ClickAsync();
        await page.Locator(OnboardingPageSelectors.SkipSrpBackupButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.OptOut).ClickAsync();
        await page.Locator(OnboardingPageSelectors.Complete.ConfirmButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.DownloadAppContinue).ClickAsync();
        await page.Locator(OnboardingPageSelectors.PinExtension.ConfirmButton).ClickAsync();
       
        await FinalizeOnboardingAsync(page, new HomePageDriver());
    }
    
    public async Task ImportWalletAsync(IPage page, string seedPhrase, string password)
    {
        await page.Locator(OnboardingPageSelectors.GetStartedButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.TermsOfUseCheckbox).ClickAsync();
        await page.Locator(OnboardingPageSelectors.TermsOfUseScrollButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.TermsOfUseAgreeButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.ImportWallet).ClickAsync();
        await page.Locator(OnboardingPageSelectors.ImportUsingSrpButton).ClickAsync();
        
        await ConfirmSecretRecoveryPhraseAsync(page, seedPhrase);
        await CreatePasswordAsync(page, password);

        await page.Locator(OnboardingPageSelectors.OptOut).ClickAsync();

        await page.Locator(OnboardingPageSelectors.Complete.ConfirmButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.DownloadAppContinue).ClickAsync();

        await page.Locator(OnboardingPageSelectors.PinExtension.NextButton).ClickAsync();
        await page.Locator(OnboardingPageSelectors.PinExtension.ConfirmButton).ClickAsync();
        
        await FinalizeOnboardingAsync(page, new HomePageDriver());
        await VerifyImportedWalletAsync(page);
    }

    public async Task ConfirmSecretRecoveryPhraseAsync(IPage page, string seedPhrase)
    {
        await page.Locator(OnboardingPageSelectors.SrpInputImportNote).PressSequentiallyAsync(seedPhrase);

        var confirmSrpButton = page.Locator(OnboardingPageSelectors.ConfirmSecretRecoveryPhraseButton);
        await confirmSrpButton.ClickAsync();
        
        if (await confirmSrpButton.CountAsync() != 0)
        {
            var errorText = await page.Locator(OnboardingPageSelectors.ImportSrpError)
                .TextContentAsync(new() { Timeout = 1_000 }) ?? "Unknown error";
            throw new Exception($"[ConfirmSecretRecoveryPhrase] Invalid seed phrase. Error from MetaMask: {errorText}");
        }
    }

    public async Task CreatePasswordAsync(IPage page, string password)
    {
        await page.Locator(OnboardingPageSelectors.CreatePasswordNewInput).FillAsync(password);
        await page.Locator(OnboardingPageSelectors.CreatePasswordConfirmInput).FillAsync(password);
        await page.Locator(OnboardingPageSelectors.PasswordStep.AcceptTermsCheckbox).ClickAsync();
        
        var errorLocator = page.Locator(OnboardingPageSelectors.CreatePasswordError);

        var createPasswordSubmitButton = page.Locator(OnboardingPageSelectors.CreatePasswordSubmit);

        if (await createPasswordSubmitButton.IsDisabledAsync())
        {
            if (await errorLocator.CountAsync() > 0)
            {
                var errorText = await errorLocator.TextContentAsync(new() { Timeout = 1_000 }) ?? "Unknown error";
                throw new Exception($"[CreatePassword] Invalid password. Error from MetaMask: {errorText}");
            }
            throw new Exception("[CreatePassword] Invalid password. No error message found in MetaMask.");
        }

        await createPasswordSubmitButton.ClickAsync();
    }
    
    public async Task FinalizeOnboardingAsync(IPage page, HomePageDriver homePageDriver)
    {
        await homePageDriver.CloseNewNetworkInfoPopoverAsync(page);
        await homePageDriver.ClosePopoverAsync(page);
        await homePageDriver.CloseWhatsNewPopoverAsync(page);
    }

    public async Task VerifyImportedWalletAsync(IPage page)
    {
        var accountAddress = await page.Locator(HomePageSelectors.CopyAccountAddressButton).TextContentAsync();

        if (string.IsNullOrEmpty(accountAddress) || !accountAddress.StartsWith("0x"))
        {
            throw new Exception(
                $"Incorrect state after importing the seed phrase. Account address is expected to start with \"0x\", but got \"{accountAddress}\" instead.\n" +
                "Note: Try to re-run the cache creation. This is a known but rare error where MetaMask hangs during the onboarding process. If it persists, please file an issue on GitHub."
            );
        }
    }
    
}