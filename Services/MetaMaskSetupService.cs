using System.Text.Json;
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
    private const int ProfileReleaseTimeoutMs = 20_000;
    private const int MetaMaskPageTimeoutMs = 30_000;

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
    private string _cacheDiscriminator = string.Empty;

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

    /// <summary>
    /// Distinguishes create-new-wallet cache entries. Required for caching when no seed phrase
    /// is set (each create-new run generates a different wallet). Without a discriminator,
    /// create-new setups skip cache read and write to avoid colliding on a shared key.
    /// </summary>
    public MetaMaskSetupService WithCacheDiscriminator(string discriminator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);
        _cacheDiscriminator = discriminator;
        return this;
    }

    public async Task<MetaMaskSetupResult> SetupAsync()
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

    private async Task<MetaMaskSetupResult> SetupInternalAsync()
    {
        if (string.IsNullOrEmpty(_password))
        {
            throw new InvalidOperationException("Password must be provided using WithPassword()");
        }

        ValidateExtensionVersion();

        var contextCachePathIsProfile = IsProfileDirectory(_contextCachePath);
        if (contextCachePathIsProfile)
        {
            _userProfilePath = _contextCachePath;
        }

        var cachingEnabled = IsCachingEnabled();
        if (_useContextCacheIfExists && !cachingEnabled && IsCreateNewWithoutDiscriminator())
        {
            Console.WriteLine(
                "[MetaMaskSetupService] Create-new wallet without WithCacheDiscriminator — " +
                "caching disabled to avoid collisions between different random wallets.");
        }

        var cacheBasePath = contextCachePathIsProfile ? DefaultContextCachePath : _contextCachePath;
        var cacheKey = ComputeCacheKey();
        var cachePath = Path.Combine(cacheBasePath, cacheKey);
        var useExistingCache = !contextCachePathIsProfile
            && cachingEnabled
            && string.IsNullOrEmpty(_userProfilePath)
            && Directory.Exists(cachePath);

        if (useExistingCache)
        {
            _userProfilePath = cachePath;
        }

        var context = await LaunchContextAsync(_userProfilePath);

        var homePage = await WaitForMetaMaskPageAsync(context);
        await homePage.WaitForLoadStateAsync();
        await CloseNonExtensionPagesAsync(context, homePage);

        var extensionId = new Uri(homePage.Url).Host;
        await MetaMaskUtils.WaitForMetaMaskWindowToBeStableAsync(homePage);

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

        await MetaMaskUtils.WaitForMetaMaskWindowToBeStableAsync(homePage);

        if (!usedExistingContext)
        {
            if (_networkToAdd != null)
            {
                await metaMaskDriver.AddNetworkAsync(_networkToAdd);
                await MetaMaskUtils.WaitForMetaMaskWindowToBeStableAsync(homePage);
            }

            if (!string.IsNullOrEmpty(_networkToSelect))
            {
                await metaMaskDriver.SwitchNetworkAsync(_networkToSelect);
            }

            foreach (var privateKey in _privateKeysToImport)
            {
                await metaMaskDriver.ImportWalletFromPrivateKeyAsync(privateKey);
            }

            await WaitForExtensionWriteQuiescenceAsync(_tempUserProfilePath, extensionId, _extensionSaveDelayMs);
        }

        if (!usedExistingContext && !useExistingCache && cachingEnabled && !contextCachePathIsProfile)
        {
            try
            {
                await metaMaskDriver.LockWalletAsync();
                await metaMaskDriver.UnlockWalletAsync();
                await homePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await MetaMaskUtils.WaitForMetaMaskWindowToBeStableAsync(homePage);
                await WaitForExtensionWriteQuiescenceAsync(_tempUserProfilePath, extensionId, _extensionSaveDelayMs);

                await context.CloseAsync();
                await WaitForExtensionStorageReleaseAsync(_tempUserProfilePath, extensionId, ProfileReleaseTimeoutMs);

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
            var relaunchedHomePage = await WaitForMetaMaskPageAsync(context);
            await relaunchedHomePage.WaitForLoadStateAsync();
            await MetaMaskUtils.WaitForMetaMaskWindowToBeStableAsync(relaunchedHomePage);

            extensionId = new Uri(relaunchedHomePage.Url).Host;
            var relaunchedDriver = new MetaMaskDriver(context, relaunchedHomePage, _password, extensionId);
            await relaunchedDriver.UnlockWalletAsync();
        }

        return new MetaMaskSetupResult(context, extensionId);
    }

    public async Task CleanupAsync(MetaMaskSetupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        await CleanupAsync(result.Context);
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

    private void ValidateExtensionVersion()
    {
        var manifestPath = Path.Combine(_metamaskExtensionPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                $"MetaMask extension manifest not found at '{manifestPath}'. " +
                $"Provide an unpacked MetaMask {Constants.METAMASK_VERSION} directory containing manifest.json. " +
                "See MetaMaskDownloadManager (NuGet) to download a matching build.");
        }

        string? version;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            version = doc.RootElement.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse MetaMask manifest.json at '{manifestPath}': {ex.Message}", ex);
        }

        if (!string.Equals(version, Constants.METAMASK_VERSION, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"MetaMask extension version mismatch. This library supports {Constants.METAMASK_VERSION} " +
                $"but the extension at '{_metamaskExtensionPath}' reports '{version ?? "(missing)"}'. " +
                "Download the matching unpacked build (e.g. via MetaMaskDownloadManager) before calling SetupAsync.");
        }
    }

    private bool IsCreateNewWithoutDiscriminator() =>
        string.IsNullOrEmpty(_seedPhrase) && string.IsNullOrEmpty(_cacheDiscriminator);

    private bool IsCachingEnabled() =>
        _useContextCacheIfExists && !IsCreateNewWithoutDiscriminator();

    private string ComputeCacheKey() =>
        MetaMaskCacheKey.Compute(
            _seedPhrase,
            _password,
            Constants.METAMASK_VERSION,
            _networkToAdd,
            _networkToSelect,
            _privateKeysToImport,
            _cacheDiscriminator);

    private static async Task<IPage> WaitForMetaMaskPageAsync(IBrowserContext context, int timeoutMs = MetaMaskPageTimeoutMs)
    {
        var existing = context.Pages.FirstOrDefault(IsMetaMaskExtensionPage);
        if (existing != null)
            return existing;

        var page = await context.WaitForPageAsync(new BrowserContextWaitForPageOptions
        {
            Predicate = IsMetaMaskExtensionPage,
            Timeout = timeoutMs
        });

        return page;
    }

    private static bool IsMetaMaskExtensionPage(IPage page) =>
        page.Url.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase);

    private static async Task CloseNonExtensionPagesAsync(IBrowserContext context, IPage metaMaskPage)
    {
        foreach (var page in context.Pages.ToArray())
        {
            if (page == metaMaskPage || IsMetaMaskExtensionPage(page))
                continue;

            try
            {
                await page.CloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MetaMaskSetupService] Failed to close non-extension page: {ex.Message}");
            }
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

    /// <summary>
    /// Waits until MetaMask's extension storage has been quiescent (no file-system
    /// writes) for <paramref name="quietWindowMs"/> milliseconds, using a two-phase
    /// strategy that is safe regardless of when MetaMask's async write begins.
    /// </summary>
    private static async Task WaitForExtensionWriteQuiescenceAsync(
        string profilePath,
        string extensionId,
        int quietWindowMs = 2_000,
        int timeoutMs = 15_000,
        int firstWriteGracePeriodMs = 3_000)
    {
        var candidatePaths = new[]
        {
            Path.Combine(profilePath, "Default", "IndexedDB",
                $"chrome-extension_{extensionId}_0.indexeddb.leveldb"),
            Path.Combine(profilePath, "Default", "Local Extension Settings", extensionId),
        };

        var watchPath = candidatePaths.FirstOrDefault(Directory.Exists)
            ?? Path.Combine(profilePath, "Default");

        if (!Directory.Exists(watchPath))
        {
            await Task.Delay(quietWindowMs);
            return;
        }

        Console.WriteLine($"[MetaMaskSetupService] Waiting for extension writes to quiesce (quiet window {quietWindowMs}ms)...");

        var firstWriteSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var quiescent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Timer?[] timerHolder = [null];

        FileSystemEventHandler onEvent = (_, _) =>
        {
            firstWriteSeen.TrySetResult(true);
            timerHolder[0]?.Change(quietWindowMs, Timeout.Infinite);
        };

        using var watcher = new FileSystemWatcher(watchPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            InternalBufferSize = 65536,
            EnableRaisingEvents = true
        };

        watcher.Changed += onEvent;
        watcher.Created += onEvent;
        watcher.Deleted += onEvent;

        try
        {
            await Task.WhenAny(firstWriteSeen.Task, Task.Delay(firstWriteGracePeriodMs));

            if (!firstWriteSeen.Task.IsCompleted)
            {
                Console.WriteLine("[MetaMaskSetupService] No extension writes observed — state assumed already persisted.");
                return;
            }

            timerHolder[0] = new Timer(_ => quiescent.TrySetResult(true), null, quietWindowMs, Timeout.Infinite);

            var remainingMs = Math.Max(quietWindowMs, timeoutMs - firstWriteGracePeriodMs);
            await Task.WhenAny(quiescent.Task, Task.Delay(remainingMs));

            if (quiescent.Task.IsCompleted)
                Console.WriteLine("[MetaMaskSetupService] Extension writes quiescent — state persisted.");
            else
                Console.WriteLine($"[MetaMaskSetupService] Warning: extension write quiescence timed out after {timeoutMs}ms — proceeding.");
        }
        finally
        {
            watcher.Changed -= onEvent;
            watcher.Created -= onEvent;
            watcher.Deleted -= onEvent;
            timerHolder[0]?.Dispose();
        }
    }

    private static async Task WaitForExtensionStorageReleaseAsync(
        string profilePath,
        string extensionId,
        int timeoutMs = 20_000,
        int pollIntervalMs = 100)
    {
        var lockPath = Path.Combine(
            profilePath, "Default", "Local Extension Settings", extensionId, "LOCK");

        if (!File.Exists(lockPath))
        {
            Console.WriteLine("[MetaMaskSetupService] Extension storage LOCK not found — falling back to profile quiescence polling.");
            await WaitForProfileQuiescenceAsync(profilePath, timeoutMs, pollIntervalMs * 5);
            return;
        }

        Console.WriteLine("[MetaMaskSetupService] Waiting for Chromium to release extension storage lock...");
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var fs = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None);
                Console.WriteLine("[MetaMaskSetupService] Extension storage lock released — profile flush complete.");
                return;
            }
            catch (IOException)
            {
                await Task.Delay(pollIntervalMs);
            }
        }

        Console.WriteLine($"[MetaMaskSetupService] Warning: timed out after {timeoutMs}ms waiting for extension storage lock release — proceeding anyway.");
    }

    private static async Task WaitForProfileQuiescenceAsync(
        string profilePath,
        int timeoutMs,
        int pollIntervalMs)
    {
        Console.WriteLine("[MetaMaskSetupService] Waiting for profile directory to stabilise...");
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        long prevSize = -1;
        int stableReads = 0;
        const int requiredStableReads = 2;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollIntervalMs);

            long currentSize = Directory
                .EnumerateFiles(profilePath, "*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch (IOException) { return 0L; } });

            if (currentSize == prevSize)
            {
                if (++stableReads >= requiredStableReads)
                {
                    Console.WriteLine("[MetaMaskSetupService] Profile directory stable — flush complete.");
                    return;
                }
            }
            else
            {
                stableReads = 0;
                prevSize = currentSize;
            }
        }

        Console.WriteLine($"[MetaMaskSetupService] Warning: timed out after {timeoutMs}ms waiting for profile quiescence — proceeding anyway.");
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

    private static bool IsProfileDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;
        return Directory.Exists(Path.Combine(path, "Default"));
    }
}
