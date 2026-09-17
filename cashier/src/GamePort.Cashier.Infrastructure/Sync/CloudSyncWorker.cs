using GamePort.Cashier.Application.UseCases.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Sync;

public class CloudSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CloudOptions> _options;
    private readonly ILogger<CloudSyncWorker> _logger;

    public CloudSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<CloudOptions> options,
        ILogger<CloudSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(10, _options.Value.SyncIntervalSeconds));

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

                var outbox = scope.ServiceProvider.GetRequiredService<ProcessOutboxHandler>();
                var outboxResult = await outbox.HandleAsync(stoppingToken);
                if (outboxResult.Pushed > 0 || outboxResult.Failed > 0)
                {
                    _logger.LogInformation("Sync outbox: pushed {Pushed}, failed {Failed}.", outboxResult.Pushed, outboxResult.Failed);
                }

                var inbox = scope.ServiceProvider.GetRequiredService<ProcessInboxHandler>();
                var inboxResult = await inbox.HandleAsync(stoppingToken);
                if (inboxResult.Applied > 0 || inboxResult.Failed > 0)
                {
                    _logger.LogInformation(
                        "Sync inbox: received {Received}, applied {Applied}, skipped {Skipped}, failed {Failed}.",
                        inboxResult.Received, inboxResult.Applied, inboxResult.Skipped, inboxResult.Failed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloud sync iteration failed.");
            }
        }
    }
}
