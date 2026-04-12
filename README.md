# MetamaskSetup.Playwright

Library for **end-to-end and integration tests** that need a real MetaMask wallet inside **Chromium**, driven by [Playwright for .NET](https://playwright.dev/dotnet/). It launches a **persistent browser profile** with the MetaMask extension loaded, then walks through onboarding and wallet management the same way a user would—so your DApp or web3 UI can be exercised against an actual extension, not mocks.

## What you can automate

| Area | Capabilities |
|------|----------------|
| **Browser** | Launch Chromium with MetaMask via `--load-extension` / persistent context (required for extensions; not headless). |
| **Wallet** | Create a new wallet, import from a seed phrase, unlock after restarts, lock/unlock when saving cached profiles. |
| **Networks** | Add a custom network (RPC, chain ID, symbol, optional block explorer) and switch networks (popular, additional, or custom lists). |
| **Accounts** | Import additional accounts from private keys; switch account by name or address (via the extension UI). |
| **Performance** | Optional **on-disk profile cache**: after a full setup, copy the user profile to a cache so the next run can reuse it and only unlock (faster CI runs when the scenario allows). |
| **Lifecycle** | `SetupAsync` returns an `IBrowserContext` for your tests; `CleanupAsync` closes the context and removes temporary profiles. |

The public surface is centered on **`MetaMaskSetupService`** (fluent configuration) and Playwright’s **`IBrowserContext`** / **`IPage`** for your application under test.

## Supported MetaMask version

This package supports **one** MetaMask extension build per release (currently **13.2.3**, see `Core.Meta.Constants.METAMASK_VERSION`). All UI flows use **selectors** that match that build’s UI. You must load an **unpacked** extension directory that matches that version. When MetaMask’s UI changes, a new **library release** will update selectors and the constant—consumers should not mix arbitrary extension versions with the package.

## Getting the MetaMask extension

This package does **not** download or bundle MetaMask. Install the correct Chrome build yourself (match the version above).

A practical option is **[MetaMask Download Manager](https://github.com/DApp-Automation-Tools/metamask-download-manager-net)** ([NuGet: `MetaMaskDownloadManager`](https://www.nuget.org/packages/MetaMaskDownloadManager/)), which can download MetaMask releases from GitHub (specific version or latest), list available versions, and optionally use a `GITHUB_TOKEN` to reduce API rate limits. See the project’s [README](https://github.com/DApp-Automation-Tools/metamask-download-manager-net/blob/main/README.md) for setup and examples.

After download, point the setup service at the **unpacked** extension folder (the directory that contains `manifest.json`).

## Requirements

- .NET 8+
- Unpacked MetaMask **matching** the supported version.
- **Non-headless** Chromium (MetaMask does not run in headless mode).

## Install

```bash
dotnet add package MetamaskSetup.Playwright
```

## Quick usage

```csharp
using Core.Services;
using Microsoft.Playwright;

var playwright = await Playwright.CreateAsync();
var browserType = playwright.Chromium;

var setup = new MetaMaskSetupService(browserType, @"C:\path\to\unpacked-metamask-13.2.3")
    .WithPassword("your-secure-password");

var context = await setup.SetupAsync();
try
{
    // Use context.Pages / new pages for your app under test
}
finally
{
    await setup.CleanupAsync(context);
}
```

## License

MIT (see package metadata).
