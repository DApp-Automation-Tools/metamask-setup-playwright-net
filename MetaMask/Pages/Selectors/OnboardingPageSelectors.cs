namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class OnboardingPageSelectors
{
    public static string GetStartedButton =>SelectorCreator.DataTestIdSelector("onboarding-get-started-button");
    
    public static string OptIn => SelectorCreator.DataTestIdSelector("metametrics-i-agree");
    public static string OptOut => SelectorCreator.DataTestIdSelector("metametrics-no-thanks");

    public static string TermsOfUseCheckbox => SelectorCreator.IdSelector("terms-of-use__checkbox");
    public static string OnboardingTermsCheckbox => SelectorCreator.DataTestIdSelector("onboarding-terms-checkbox");
    public static string TermsOfUseScrollButton => SelectorCreator.DataTestIdSelector("terms-of-use-scroll-button");
    public static string TermsOfUseAgreeButton => SelectorCreator.DataTestIdSelector("terms-of-use-agree-button");
    
    public static string CreateNewWallet => SelectorCreator.DataTestIdSelector("onboarding-create-wallet");
    public static string ImportWallet => SelectorCreator.DataTestIdSelector("onboarding-import-wallet");
    public static string ImportUsingSrpButton => SelectorCreator.DataTestIdSelector("onboarding-import-with-srp-button");
    public static string CreateUsingSrpButton => SelectorCreator.DataTestIdSelector("onboarding-create-with-srp-button");

    public static string SecureWalletLaterButton => SelectorCreator.DataTestIdSelector("secure-wallet-later");
    public static string SrpBackupCheckbox => SelectorCreator.IdSelector("skip-srp-backup__checkbox");
    public static string SkipSrpBackupButton => SelectorCreator.DataTestIdSelector("skip-srp-backup-button");
    public static string SrpInputImportNote => SelectorCreator.DataTestIdSelector("srp-input-import__srp-note");
    public static string ConfirmSecretRecoveryPhraseButton => SelectorCreator.DataTestIdSelector("import-srp-confirm");
    public static string ImportSrpError => SelectorCreator.DataTestIdSelector("import-srp-error");
    public static string CreatePasswordNewInput => SelectorCreator.DataTestIdSelector("create-password-new-input");

    public static string CreatePasswordConfirmInput => SelectorCreator.DataTestIdSelector("create-password-confirm-input");
    public static string CreatePasswordSubmit => SelectorCreator.DataTestIdSelector("create-password-submit");

    public static string CreatePasswordError => SelectorCreator.DataTestIdSelector("confirm-password-error");
    public static string DownloadAppContinue => SelectorCreator.DataTestIdSelector("download-app-continue");
    public static class PinExtension
    {
        public static string NextButton => SelectorCreator.DataTestIdSelector("pin-extension-next");
        public static string ConfirmButton => SelectorCreator.DataTestIdSelector("pin-extension-done");
    }

    public static class PasswordStep
    {
        public static string AcceptTermsCheckbox => SelectorCreator.DataTestIdSelector("create-password-terms");
    }

    public static class Complete
    {
        public static string ConfirmButton => SelectorCreator.DataTestIdSelector("onboarding-complete-done");
    }
}