using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.Models;

/// <summary>
/// Result of a successful <c>MetaMaskSetupService.SetupAsync</c> call.
/// </summary>
/// <param name="Context">Persistent browser context with MetaMask loaded and unlocked.</param>
/// <param name="ExtensionId">
/// Chromium extension id derived from the MetaMask page URL host
/// (useful for consumers who automate notification popups themselves).
/// </param>
public sealed record MetaMaskSetupResult(IBrowserContext Context, string ExtensionId);
