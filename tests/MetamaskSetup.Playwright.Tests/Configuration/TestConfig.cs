namespace MetamaskSetup.Playwright.Tests.Configuration;

/// <summary>
/// Reads test configuration from environment variables.
///
/// The MetaMask extension path is resolved automatically by <see cref="Fixtures.MetaMaskExtensionFixture"/>:
/// set METAMASK_EXTENSION_PATH to skip the download (local dev / pre-cached CI), or let the fixture
/// download the pinned version via MetaMaskDownloadManager. Set GITHUB_TOKEN to avoid rate limits.
///
/// TEST_METAMASK_PASSWORD    — wallet password (default: TestPassword123!)
/// TEST_METAMASK_SEED        — 12-word BIP-39 seed (default: Hardhat well-known test seed)
/// TEST_METAMASK_PRIVATE_KEY — extra account private key (default: Hardhat account #1)
/// </summary>
public static class TestConfig
{
    /// <summary>
    /// Wallet password used across all tests.
    /// </summary>
    public static string Password =>
        Environment.GetEnvironmentVariable("TEST_METAMASK_PASSWORD") ?? "TestPassword123!";

    /// <summary>
    /// BIP-39 seed phrase used for wallet import tests.
    /// Defaults to the Hardhat well-known development seed — never use this with real funds.
    /// </summary>
    public static string SeedPhrase =>
        Environment.GetEnvironmentVariable("TEST_METAMASK_SEED")
        ?? "test test test test test test test test test test test junk";

    /// <summary>
    /// Ethereum private key for additional account import tests.
    /// Defaults to Hardhat account #1 — a public, zero-value test key.
    /// </summary>
    public static string PrivateKey =>
        Environment.GetEnvironmentVariable("TEST_METAMASK_PRIVATE_KEY")
        ?? "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
}
