namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class LoadingSelectors
{
    public const string Spinner = ".spinner";
    public const string LoadingOverlay = ".loading-overlay";
    public static readonly string[] LoadingIndicators =
    {
        ".loading-logo",
        ".loading-spinner",
        ".loading-overlay",
        ".loading-overlay__spinner",
        ".loading-span",
        ".loading-indicator",
        "#loading__logo",
        "#loading__spinner",
        ".mm-button-base__icon-loading",
        ".loading-swaps-quotes",
        ".loading-heartbeat"
    };
}