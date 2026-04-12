namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class ErrorSelectors
{
    public const string LoadingOverlayErrorButtons = ".loading-overlay__error-buttons";
    public const string LoadingOverlayErrorButtonsRetryButton = ".loading-overlay__error-buttons .btn-primary";
    public const string CriticalError = ".critical-error";
    public const string CriticalErrorRestartButton = "#critical-error-button";
    public const string ErrorPage = ".error-page";
}