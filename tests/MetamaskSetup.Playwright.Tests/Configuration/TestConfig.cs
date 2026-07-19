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
    /// Must NOT be derivable from <see cref="SeedPhrase"/>; using a Hardhat account key
    /// alongside the Hardhat mnemonic causes a "duplicate account" error in MetaMask.
    /// Defaults to a well-known example key (address 0x2c7536E3…65c23) — never use with real funds.
    /// </summary>
    public static string PrivateKey =>
        Environment.GetEnvironmentVariable("TEST_METAMASK_PRIVATE_KEY")
        ?? "0x4c0883a69102937d6231471b5dbb6e538eba2ef2f32e85bf3b5b39a22d2d6b3d";
}
