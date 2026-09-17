using System.Diagnostics;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GamePort.Cashier.Infrastructure.Backup;

public class PostgresBackupService : IBackupService
{
    private readonly CashierDbContext _context;
    private readonly BackupOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PostgresBackupService> _logger;

    public PostgresBackupService(
        CashierDbContext context,
        IOptions<BackupOptions> options,
        IConfiguration configuration,
        ILogger<PostgresBackupService> logger)
    {
        _context = context;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsSupported => _context.Database.IsRelational();

    public async Task<BackupResult> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return new BackupResult(false, null, "بکاپ فقط برای پایگاه‌داده PostgreSQL پشتیبانی می‌شود.");
        }

        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new BackupResult(false, null, "رشته اتصال پایگاه‌داده یافت نشد.");
        }

        var directory = ResolveDirectory();
        System.IO.Directory.CreateDirectory(directory);

        var fileName = $"wingport-{DateTime.Now:yyyyMMdd-HHmmss}.dump";
        var filePath = Path.Combine(directory, fileName);

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var arguments = new List<string>
            {
                "-Fc",
                "-h", builder.Host ?? "localhost",
                "-p", (builder.Port > 0 ? builder.Port : 5432).ToString(),
                "-U", builder.Username ?? "postgres",
                "-d", builder.Database ?? "postgres",
                "-f", filePath
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.PgDumpPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!string.IsNullOrEmpty(builder.Password))
            {
                startInfo.Environment["PGPASSWORD"] = builder.Password;
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new BackupResult(false, null, "اجرای pg_dump ممکن نشد.");
            }

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("pg_dump exited with code {Code}: {Error}", process.ExitCode, error);
                return new BackupResult(false, null, $"بکاپ ناموفق بود (کد {process.ExitCode}).");
            }

            PruneOldBackups(directory);

            _logger.LogInformation("Database backup created at {Path}.", filePath);
            return new BackupResult(true, filePath, null);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            _logger.LogError(ex, "pg_dump was not found.");
            return new BackupResult(false, null, "ابزار pg_dump یافت نشد. مسیر آن را در تنظیمات Backup:PgDumpPath مشخص کنید.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed.");
            return new BackupResult(false, null, "بکاپ با خطا مواجه شد.");
        }
    }

    public Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var directory = ResolveDirectory();
        if (!System.IO.Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<BackupInfo>>(Array.Empty<BackupInfo>());
        }

        var backups = new DirectoryInfo(directory)
            .GetFiles("*.dump")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupInfo(f.Name, f.Length, f.CreationTimeUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<BackupInfo>>(backups);
    }

    private string ResolveDirectory()
        => Path.IsPathRooted(_options.Directory)
            ? _options.Directory
            : Path.Combine(LocalPaths.Resolve(_configuration), _options.Directory);

    private void PruneOldBackups(string directory)
    {
        if (_options.RetainCount <= 0)
        {
            return;
        }

        var files = new DirectoryInfo(directory)
            .GetFiles("*.dump")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(_options.RetainCount)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup {File}.", file.Name);
            }
        }
    }
}
