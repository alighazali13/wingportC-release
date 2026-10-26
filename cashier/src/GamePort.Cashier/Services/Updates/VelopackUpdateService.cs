using System.IO;
using System.Reflection;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace GamePort.Cashier.Services.Updates;

/// <summary>
/// Velopack-based update service targeting the public GitHub
/// releases repository. All operations are non-blocking and
/// failure-tolerant — offline startup must never be prevented.
/// </summary>
public sealed class VelopackUpdateService : IAppUpdateService, IDisposable
{
    private const string GitHubOwner = "alighazali13";
    private const string GitHubRepo = "Cashier_release";
    private const string Channel = "beta";

    private readonly UpdateManager? _manager;
    private UpdateInfo _info = new();
    private CancellationTokenSource? _cts;
    private VelopackAsset? _pendingAsset;
    private readonly string _logPath;

    public event Action? StateChanged;

    public string CurrentVersion => _info.CurrentVersion;
    public string? AvailableVersion => _info.AvailableVersion;
    public UpdateState State => _info.State;

    public VelopackUpdateService()
    {
        _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GamePort", "Cashier", "Logs", "updater.log");

        try
        {
            var locator = VelopackLocator.Current;
            if (locator is null || !VelopackLocator.IsCurrentSet)
            {
                _info.CurrentVersion = GetAssemblyVersion();
                Log($"Not a Velopack install. Version={_info.CurrentVersion}");
                return;
            }

            _info.CurrentVersion = locator.CurrentlyInstalledVersion?.ToString() ?? GetAssemblyVersion();

            var source = new GithubSource(GitHubOwner, GitHubRepo, prerelease: true);
            var options = new UpdateOptions()
            {
                ExplicitChannel = Channel,
                AllowVersionDowngrade = false
            };
            _manager = new UpdateManager(source, options, locator);
            Log($"Velopack active. Version={_info.CurrentVersion} Channel={Channel} Repo={GitHubOwner}/{GitHubRepo}");
        }
        catch (Exception ex)
        {
            _info.CurrentVersion = GetAssemblyVersion();
            Log($"Velopack init failed: {ex.Message}");
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        if (_manager is null)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _info.State = UpdateState.Checking;
        _info.ErrorMessage = null;
        Log("Check started");
        NotifyStateChanged();

        try
        {
            var task = Task.Run(() => _manager.CheckForUpdates(), _cts.Token);
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), _cts.Token));

            if (completed != task)
            {
                _info.State = UpdateState.Idle;
                Log("Check timed out (10s)");
                NotifyStateChanged();
                return;
            }

            var updateInfo = await task;

            if (updateInfo?.TargetFullRelease is not null)
            {
                _pendingAsset = updateInfo.TargetFullRelease;
                _info.AvailableVersion = updateInfo.TargetFullRelease.Version.ToString();
                _info.State = UpdateState.Ready;
                Log($"Update found: {updateInfo.TargetFullRelease.Version}");
                _ = DownloadUpdateAsync();
            }
            else
            {
                _pendingAsset = null;
                _info.AvailableVersion = null;
                _info.State = UpdateState.Idle;
                Log("No update available");
            }
        }
        catch (OperationCanceledException)
        {
            _info.State = UpdateState.Idle;
            Log("Check cancelled");
        }
        catch (Exception ex)
        {
            _info.State = UpdateState.Failed;
            _info.ErrorMessage = "Update check unavailable";
            Log($"Check failed: {ex.Message}");
        }

        NotifyStateChanged();
    }

    public async Task DownloadUpdateAsync()
    {
        if (_manager is null || _pendingAsset is null)
            return;

        _info.State = UpdateState.Downloading;
        _info.DownloadProgress = 0;
        Log($"Download started for {_pendingAsset.Version}");
        NotifyStateChanged();

        try
        {
            await Task.Run(() =>
            {
                var downloadInfo = new Velopack.UpdateInfo(_pendingAsset!, false, null!, Array.Empty<VelopackAsset>());
                _manager.DownloadUpdates(downloadInfo, progress =>
                {
                    _info.DownloadProgress = progress;
                });
            });

            _info.State = UpdateState.Ready;
            Log($"Download complete for {_pendingAsset.Version}");
        }
        catch (Exception ex)
        {
            _info.State = UpdateState.Failed;
            _info.ErrorMessage = "Download failed";
            Log($"Download failed: {ex.Message}");
        }

        NotifyStateChanged();
    }

    public void ApplyUpdateAndRestart()
    {
        if (_manager is null)
        {
            Log("Apply called but manager is null");
            return;
        }

        if (_pendingAsset is null)
        {
            Log("Apply called but no pending asset — triggering fresh check");
            _info.State = UpdateState.Checking;
            NotifyStateChanged();

            try
            {
                var updateInfo = _manager.CheckForUpdates();
                if (updateInfo?.TargetFullRelease is null)
                {
                    _info.State = UpdateState.Failed;
                    _info.ErrorMessage = "نسخه جدیدی یافت نشد";
                    Log("Apply: fresh check found no update");
                    NotifyStateChanged();
                    return;
                }
                _pendingAsset = updateInfo.TargetFullRelease;
            }
            catch (Exception ex)
            {
                _info.State = UpdateState.Failed;
                _info.ErrorMessage = "نصب بروزرسانی انجام نشد. دوباره تلاش کنید.";
                Log($"Apply: fresh check failed: {ex.Message}");
                NotifyStateChanged();
                return;
            }
        }

        Log($"Apply called for {_pendingAsset!.Version}");
        try
        {
            _manager.ApplyUpdatesAndRestart(_pendingAsset);
            Log("ApplyUpdatesAndRestart invoked");
        }
        catch (Exception ex)
        {
            _info.State = UpdateState.Failed;
            _info.ErrorMessage = "نصب بروزرسانی انجام نشد. دوباره تلاش کنید.";
            Log($"Apply failed: {ex.Message}");
            NotifyStateChanged();
        }
    }

    private void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(_logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* logging must never crash the app */ }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private static string GetAssemblyVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "unknown";
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
