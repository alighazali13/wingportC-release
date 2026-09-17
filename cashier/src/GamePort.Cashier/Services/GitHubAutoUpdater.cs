using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace GamePort.Cashier.Services;

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string? DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
}

public class UpdateProgress
{
    public string Status { get; set; } = string.Empty;
    public int PercentComplete { get; set; }
}

public interface IAutoUpdater
{
    Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task<string> DownloadAndInstallAsync(string downloadUrl, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<string?> GetInstallationDirectory();
}

public class GitHubAutoUpdater : IAutoUpdater
{
    private readonly HttpClient _httpClient;
    private const string ReleasesApiUrl = "https://api.github.com/repos/alighazali13/WingPort/releases/latest";

    public GitHubAutoUpdater(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GamePort-Cashier/1.0");
    }

    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(ReleasesApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = GetCurrentVersion(),
                    LatestVersion = "n/a"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() ?? "" : "";
            var latestVersion = tagName.TrimStart('v');
            var currentVersion = GetCurrentVersion();

            var assets = root.TryGetProperty("assets", out var assetList) ? assetList : default;
            var downloadUrl = "";

            if (assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() ?? "" : "";
                        break;
                    }
                }
            }

            var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;

            return new UpdateInfo
            {
                IsUpdateAvailable = !string.IsNullOrEmpty(latestVersion) &&
                                    new Version(latestVersion) > new Version(currentVersion),
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                DownloadUrl = downloadUrl,
                ReleaseNotes = notes
            };
        }
        catch (Exception)
        {
            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = GetCurrentVersion(),
                LatestVersion = "n/a"
            };
        }
    }

    public async Task<string> DownloadAndInstallAsync(
        string downloadUrl,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("No download URL provided.");
        }

        progress?.Report(new UpdateProgress { Status = "دانلود نسخه جدید...", PercentComplete = 0 });

        var tempDir = Path.Combine(Path.GetTempPath(), "WingPort-Update", Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempDir, "update.zip");

        try
        {
            Directory.CreateDirectory(tempDir);

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(zipPath);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;
                    if (totalBytes > 0)
                    {
                        progress?.Report(new UpdateProgress
                        {
                            Status = "دانلود نسخه جدید...",
                            PercentComplete = (int)(totalRead * 100 / totalBytes)
                        });
                    }
                }
            }

            progress?.Report(new UpdateProgress { Status = "استخراج فایل‌ها...", PercentComplete = 80 });

            var installDir = await GetInstallationDirectory() ?? AppContext.BaseDirectory;
            var backupDir = Path.Combine(installDir, "Backup", $"v{GetCurrentVersion()}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupDir);

            foreach (var file in new[] { "GamePort.Cashier.exe", "GamePort.Cashier.dll", "appsettings.json" })
            {
                var src = Path.Combine(installDir, file);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(backupDir, file), true);
                }
            }

            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);
            progress?.Report(new UpdateProgress { Status = "نصب فایل‌ها...", PercentComplete = 90 });

            var extractedFiles = Directory.GetFiles(tempDir)
                .Where(f => !f.EndsWith("update.zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var extracted in extractedFiles)
            {
                var destFile = Path.Combine(installDir, Path.GetFileName(extracted));
                File.Copy(extracted, destFile, true);
            }

            progress?.Report(new UpdateProgress { Status = "تکمیل شد.", PercentComplete = 100 });

            return tempDir;
        }
        catch
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            throw;
        }
    }

    public Task<string?> GetInstallationDirectory()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var location = assembly.Location;

        if (string.IsNullOrEmpty(location))
        {
            return Task.FromResult<string?>(AppContext.BaseDirectory);
        }

        return Task.FromResult<string?>(Path.GetDirectoryName(location));
    }

    public static string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}
