namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class SelectorCreator
{
    public static string DataTestIdSelector(string name) => $"[data-testid=\"{name}\"]";
    public static string IdSelector(string name) => $"[id=\"{name}\"]";
}