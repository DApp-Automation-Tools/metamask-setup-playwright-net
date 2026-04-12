namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class LockPageSelectors
{
    public static string PasswordInput => SelectorCreator.DataTestIdSelector("unlock-password");
    public static string SubmitButton => SelectorCreator.DataTestIdSelector("unlock-submit");
}