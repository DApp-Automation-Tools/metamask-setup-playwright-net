using System.Security.Cryptography;
using System.Text;
using MetamaskSetup.Playwright.Models;

namespace MetamaskSetup.Playwright.Services;

/// <summary>
/// Builds the on-disk profile cache key from setup parameters.
/// Extracted for unit testing without launching a browser.
/// </summary>
internal static class MetaMaskCacheKey
{
    public static string Compute(
        string? seedPhrase,
        string password,
        string metamaskVersion,
        NetworkConfig? networkToAdd,
        string? networkToSelect,
        IEnumerable<string> privateKeys,
        string? cacheDiscriminator)
    {
        var sb = new StringBuilder();
        sb.Append(string.IsNullOrEmpty(seedPhrase) ? "new" : seedPhrase);
        sb.Append('|');
        sb.Append(password);
        sb.Append('|');
        sb.Append(metamaskVersion);
        sb.Append('|');
        if (networkToAdd != null)
        {
            sb.Append(networkToAdd.Name);
            sb.Append(networkToAdd.RpcUrl);
            sb.Append(networkToAdd.ChainId);
        }
        sb.Append('|');
        sb.Append(networkToSelect ?? string.Empty);
        sb.Append('|');
        foreach (var pk in privateKeys)
            sb.Append(pk);
        sb.Append('|');
        sb.Append(cacheDiscriminator ?? string.Empty);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16];
    }
}
