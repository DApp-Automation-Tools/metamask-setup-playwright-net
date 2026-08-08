using MetamaskSetup.Playwright.Meta;
using MetamaskSetup.Playwright.Models;
using MetamaskSetup.Playwright.Services;

namespace MetamaskSetup.Playwright.Tests.Tests;

/// <summary>
/// Unit tests for cache key composition — no browser required.
/// </summary>
public sealed class CacheKeyUnitTests
{
    [Fact]
    public void Compute_EmptySeed_UsesLiteralNew()
    {
        var withEmpty = MetaMaskCacheKey.Compute(
            seedPhrase: "",
            password: "pw",
            metamaskVersion: Constants.METAMASK_VERSION,
            networkToAdd: null,
            networkToSelect: null,
            privateKeys: [],
            cacheDiscriminator: "a");

        var withNull = MetaMaskCacheKey.Compute(
            seedPhrase: null,
            password: "pw",
            metamaskVersion: Constants.METAMASK_VERSION,
            networkToAdd: null,
            networkToSelect: null,
            privateKeys: [],
            cacheDiscriminator: "a");

        Assert.Equal(withEmpty, withNull);
    }

    [Fact]
    public void Compute_DifferentDiscriminators_ProduceDifferentKeys()
    {
        var key1 = MetaMaskCacheKey.Compute("", "pw", Constants.METAMASK_VERSION, null, null, [], "run-1");
        var key2 = MetaMaskCacheKey.Compute("", "pw", Constants.METAMASK_VERSION, null, null, [], "run-2");
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Compute_DifferentNetworkToSelect_ProduceDifferentKeys()
    {
        var network = new NetworkConfig
        {
            Name = "Local",
            RpcUrl = "http://127.0.0.1:8545",
            ChainId = "31337",
            Symbol = "ETH"
        };

        var key1 = MetaMaskCacheKey.Compute(
            "seed words", "pw", Constants.METAMASK_VERSION, network, "Local", [], null);
        var key2 = MetaMaskCacheKey.Compute(
            "seed words", "pw", Constants.METAMASK_VERSION, network, "Ethereum Mainnet", [], null);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Compute_SameParameters_ProduceSameKey()
    {
        var key1 = MetaMaskCacheKey.Compute(
            "seed", "pw", Constants.METAMASK_VERSION, null, "Local", ["0xabc"], "d");
        var key2 = MetaMaskCacheKey.Compute(
            "seed", "pw", Constants.METAMASK_VERSION, null, "Local", ["0xabc"], "d");
        Assert.Equal(key1, key2);
        Assert.Equal(16, key1.Length);
    }
}
