using System.Security.Cryptography;
using System.Text;
using MetamaskSetup.Playwright.Meta;
using MetamaskSetup.Playwright.MetaMask;
using MetamaskSetup.Playwright.Models;
using MetamaskSetup.Playwright.Utils;
using Microsoft.Playwright;

namespace MetamaskSetup.Playwright.Services;

public class MetaMaskSetupService
{
    private const string DefaultContextCachePath = "./cache/metamask-profiles";
    private const int DefaultExtensionSaveDelayMs = 2000;
    private const int ContextCloseDelayMs = 1000;

    private readonly IBrowserType _browserType;
    private readonly string _metamaskExtensionPath;

    private string _seedPhrase = string.Empty;
    private string _password = string.Empty;
    private NetworkConfig? _networkToAdd;
    private string _networkToSelect = string.Empty;
    private string _userProfilePath = string.Empty;
    private readonly List<string> _privateKeysToImport = [];
    private string _tempUserProfilePath = string.Empty;
    private string _contextCachePath = DefaultContextCachePath;
    private bool _useContextCacheIfExists = true;
    private int _extensionSaveDelayMs = DefaultExtensionSaveDelayMs;

    public MetaMaskSetupService(IBrowserType browserType, string metamaskExtensionPath)
    {
        ArgumentNullException.ThrowIfNull(browserType);
        ArgumentException.ThrowIfNullOrWhiteSpace(metamaskExtensionPath);
        _browserType = browserType;
        _metamaskExtensionPath = metamaskExtensionPath;
    }

    public MetaMaskSetupService FromUserProfile(string userProfilePath)
    {
        _userProfilePath = userProfilePath;
        return this;
    }

    public MetaMaskSetupService WithSeedPhrase(string seedPhrase)
    {
        _seedPhrase = seedPhrase;
        return this;
    }

    public MetaMaskSetupService WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public MetaMaskSetupService WithAdditionalAccounts(params string[] privateKeys)
    {
        if (privateKeys.Length > 0)
        {
            _privateKeysToImport.AddRange(privateKeys);
        }
        return this;
    }

    public MetaMaskSetupService WithNetworkToAdd(NetworkConfig networkConfig)
    {
        _networkToAdd = networkConfig;
        return this;
    }

    public MetaMaskSetupService WithNetworkToSelect(string networkName)
    {
        _networkToSelect = networkName;
        return this;
    }

    public MetaMaskSetupService WithContextCachePath(string path)
    {
        _contextCachePath = path;
        return this;
    }

    public MetaMaskSetupService UseContextCacheIfExists(bool useCache = true)
    {
        _useContextCacheIfExists = useCache;
        return this;
    }

    public MetaMaskSetupService WithExtensionSaveDelayMs(int ms)
    {
        _extensionSaveDelayMs = ms;
        return this;
    }

    public async Task<IBrowserContext> SetupAsync()
    {
        try
        {
            return await SetupInternalAsync();
        }
        catch
        {
            CleanupTempProfile();
            throw;
        }
    }

    private async Task<IBrowserContext> SetupInternalAsync()
    {
        if (string.IsNullOrEmpty(_password))
        {
            throw new InvalidOperationException("Password must be provided using WithPassword()");
        }

        var contextCachePathIsProfile = IsProfileDirectory(_contextCachePath);
        if (contextCachePathIsProfile)
        {
            _userProfilePath = _contextCachePath;
        }

        var cacheBasePath = contextCachePathIsProfile ? DefaultContextCachePath : _contextCachePath;
        var cacheKey = ComputeCacheKey();
        var cachePath = Path.Combine(cacheBasePath, cacheKey);
        var useExistingCache = !contextCachePathIsProfile && _useContextCacheIfExists && string.IsNullOrEmpty(_userProfilePath) && Directory.Exists(cachePath);

        if (useExistingCache)
        {
            _userProfilePath = cachePath;
        }

        var context = await LaunchContextAsync(_userProfilePath);

        var homePage = await context.WaitForPageAsync();
        await homePage.WaitForLoadStateAsync();

        var firstPage = context.Pages.FirstOrDefault();
        if (firstPage != null && firstPage != homePage)
        {
            await firstPage.CloseAsync();
        }

        var extensionId = new Uri(homePage.Url).Host;
        await WaitUtils.WaitUntilStableAsync(homePage);

        var metaMaskDriver = new MetaMaskDriver(context, homePage, _password, extensionId);

        var usedExistingContext = !string.IsNullOrEmpty(_userProfilePath);

        if (usedExistingContext)
        {
            await metaMaskDriver.UnlockWalletAsync();
        }
        else if (!string.IsNullOrEmpty(_seedPhrase))
        {
            await metaMaskDriver.ImportWalletAsync(_seedPhrase);
        }
        else
        {
            await metaMaskDriver.CreateNewWalletAsync(_password);
        }

        if (!usedExistingContext)
        {
            if (_networkToAdd != null)
            {
                await metaMaskDriver.AddNetworkAsync(_networkToAdd);
            }

            if (!string.IsNullOrEmpty(_networkToSelect))
            {
                await metaMaskDriver.SwitchNetworkAsync(_networkToSelect);
            }

            foreach (var privateKey in _privateKeysToImport)
            {
                await metaMaskDriver.ImportWalletFromPrivateKeyAsync(privateKey);
            }

            await Task.Delay(_extensionSaveDelayMs);
        }

        if (!usedExistingContext && !useExistingCache && _useContextCacheIfExists && !contextCachePathIsProfile)
        {
            try
            {
                await metaMaskDriver.LockWalletAsync();
                await metaMaskDriver.UnlockWalletAsync();
                await homePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await WaitUtils.WaitUntilStableAsync(homePage);
                await Task.Delay(_extensionSaveDelayMs);

                await context.CloseAsync();
                await Task.Delay(ContextCloseDelayMs);

                var cacheDir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(cacheDir))
                    Directory.CreateDirectory(cacheDir);
                CopyDirectory(_tempUserProfilePath, cachePath, true);
                Console.WriteLine($"[MetaMaskSetupService] Context saved to cache: {cachePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetaMaskSetupService] Failed to save context to cache: {ex.Message}");
                throw;
            }

            context = await LaunchFromPathAsync(_tempUserProfilePath);
            var relaunchedHomePage = await context.WaitForPageAsync();
            await relaunchedHomePage.WaitForLoadStateAsync();
            await WaitUtils.WaitUntilStableAsync(relaunchedHomePage);

            var relaunchedDriver = new MetaMaskDriver(context, relaunchedHomePage, _password, new Uri(relaunchedHomePage.Url).Host);
            await relaunchedDriver.UnlockWalletAsync();
        }

        return context;
    }

    public async Task CleanupAsync(IBrowserContext context)
    {
        try
        {
            await context.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MetaMaskSetupService] Failed to close context: {ex.Message}");
        }

        CleanupTempProfile();
    }

    private void CleanupTempProfile()
    {
        if (string.IsNullOrEmpty(_tempUserProfilePath) || !Directory.Exists(_tempUserProfilePath))
            return;

        try
        {
            Directory.Delete(_tempUserProfilePath, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MetaMaskSetupService] Failed to delete temp profile: {ex.Message}");
        }
    }

    private async Task<IBrowserContext> LaunchFromPathAsync(string profilePath)
    {
        var launchArgs = new List<string>
        {
            $"--disable-extensions-except={_metamaskExtensionPath}",
            $"--load-extension={_metamaskExtensionPath}"
        };

        var context = await _browserType.LaunchPersistentContextAsync(profilePath, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            Args = launchArgs.ToArray()
        });

        return context;
    }

    private async Task<IBrowserContext> LaunchContextAsync(string? userDataDir)
    {
        var launchArgs = new List<string>
        {
            $"--disable-extensions-except={_metamaskExtensionPath}",
            $"--load-extension={_metamaskExtensionPath}"
        };

        _tempUserProfilePath = Path.Combine(Path.GetTempPath(), "playwright_metamask_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempUserProfilePath);

        if (!string.IsNullOrEmpty(userDataDir))
        {
            // Copy the existing user data directory to the temporary one
            CopyDirectory(userDataDir, _tempUserProfilePath, true);
        }

        var context = await _browserType.LaunchPersistentContextAsync(_tempUserProfilePath, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            Args = launchArgs.ToArray()
        });

        Console.WriteLine($"MetaMask User Data Directory: {_tempUserProfilePath}");

        return context;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, overwrite: true);
        }

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    private string ComputeCacheKey()
    {
        var sb = new StringBuilder();
        sb.Append(_seedPhrase ?? "new");
        sb.Append('|');
        sb.Append(_password);
        sb.Append('|');
        sb.Append(Constants.METAMASK_VERSION);
        sb.Append('|');
        if (_networkToAdd != null)
        {
            sb.Append(_networkToAdd.Name);
            sb.Append(_networkToAdd.RpcUrl);
            sb.Append(_networkToAdd.ChainId);
        }
        sb.Append('|');
        foreach (var pk in _privateKeysToImport)
            sb.Append(pk);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16];
    }

    private static bool IsProfileDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;
        return Directory.Exists(Path.Combine(path, "Default"));
    }
}