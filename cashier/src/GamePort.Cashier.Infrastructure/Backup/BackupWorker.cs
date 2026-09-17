using GamePort.Cashier.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Backup;

public class BackupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackupOptions> _options;
    private readonly ILogger<BackupWorker> _logger;

    public BackupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackupOptions> options,
        ILogger<BackupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.Value.IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                if (!backupService.IsSupported)
                {
                    continue;
                }

                var result = await backupService.CreateBackupAsync(stoppingToken);
                if (result.Success)
                {
                    _logger.LogInformation("Scheduled backup completed: {Path}.", result.FilePath);
                }
                else
                {
                    _logger.LogWarning("Scheduled backup failed: {Error}.", result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup iteration failed.");
            }
        }
    }
}
