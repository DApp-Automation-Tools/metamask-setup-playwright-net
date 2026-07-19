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
    private const int ProfileReleaseTimeoutMs = 20_000;

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

            await WaitForExtensionWriteQuiescenceAsync(_tempUserProfilePath, extensionId, _extensionSaveDelayMs);
        }

        if (!usedExistingContext && !useExistingCache && _useContextCacheIfExists && !contextCachePathIsProfile)
        {
            try
            {
                await metaMaskDriver.LockWalletAsync();
                await metaMaskDriver.UnlockWalletAsync();
                await homePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await WaitUtils.WaitUntilStableAsync(homePage);
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

    /// <summary>
    /// Waits until MetaMask's extension storage has been quiescent (no file-system
    /// writes) for <paramref name="quietWindowMs"/> milliseconds, using a two-phase
    /// strategy that is safe regardless of when MetaMask's async write begins.
    /// <para>
    /// MetaMask commits vault state to disk asynchronously after each UI operation.
    /// The write may start anywhere from immediately to several seconds after the
    /// UI settles — so a plain quiet timer started right after the UI operation
    /// would fire prematurely if MetaMask hasn't started writing yet.
    /// </para>
    /// <para>
    /// <b>Phase 1</b> waits up to <paramref name="firstWriteGracePeriodMs"/> for
    /// the first write event. If no write is observed, the state was already committed
    /// during the UI interaction itself and we return immediately.
    /// <b>Phase 2</b> (entered only when a write is detected) arms a quiet-window
    /// timer that resets on every subsequent event and fires when
    /// <paramref name="quietWindowMs"/> of silence has passed.
    /// </para>
    /// </summary>
    private static async Task WaitForExtensionWriteQuiescenceAsync(
        string profilePath,
        string extensionId,
        int quietWindowMs = 2_000,
        int timeoutMs = 15_000,
        int firstWriteGracePeriodMs = 3_000)
    {
        // Prefer the narrowest scope that covers MetaMask's writes to reduce noise
        // from unrelated Chromium background activity.
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
            // Profile not yet initialised — use the quiet window as a minimum delay.
            await Task.Delay(quietWindowMs);
            return;
        }

        Console.WriteLine($"[MetaMaskSetupService] Waiting for extension writes to quiesce (quiet window {quietWindowMs}ms)...");

        var firstWriteSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var quiescent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // timerHolder[0] is null during phase 1. Event handlers call Change() on it:
        // a null-conditional Change() is a safe no-op. The timer is created at the start
        // of phase 2, at which point subsequent events reset it correctly.
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
            // Phase 1 — wait for MetaMask to begin writing, or exhaust the grace period.
            await Task.WhenAny(firstWriteSeen.Task, Task.Delay(firstWriteGracePeriodMs));

            if (!firstWriteSeen.Task.IsCompleted)
            {
                // No writes observed in the grace period: state was committed during
                // the preceding UI operation (e.g. during WaitUntilStableAsync).
                Console.WriteLine("[MetaMaskSetupService] No extension writes observed — state assumed already persisted.");
                return;
            }

            // Phase 2 — write burst started; arm the quiet timer and wait for silence.
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

    /// <summary>
    /// Waits until Chromium has fully released the MetaMask extension's LevelDB storage lock,
    /// which is a reliable signal that the browser process has exited and the profile has been
    /// completely flushed to disk. This replaces a fixed delay after <c>context.CloseAsync()</c>.
    /// Falls back to directory size quiescence polling when the LOCK file cannot be located.
    /// </summary>
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
            // LOCK file absent means the extension storage path doesn't match what we expect.
            // Fall back to polling the whole profile directory for write quiescence so we
            // never proceed to CopyDirectory before the profile is fully flushed.
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
                // LevelDB holds this file exclusively while the database is open.
                // A successful exclusive open means Chromium has fully exited and the profile is flushed.
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

    /// <summary>
    /// Fallback flush detection: polls the total byte size of the profile directory until it
    /// remains unchanged across two consecutive reads, indicating Chromium has stopped writing.
    /// </summary>
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