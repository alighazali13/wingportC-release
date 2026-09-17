using GamePort.Cashier.Application.UseCases.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class SessionBillingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CashierServerOptions> _options;
    private readonly ILogger<SessionBillingWorker> _logger;

    public SessionBillingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<CashierServerOptions> options,
        ILogger<SessionBillingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(10, _options.Value.BillingIntervalSeconds));

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
                var handler = scope.ServiceProvider.GetRequiredService<ChargeActiveSessionsHandler>();
                var result = await handler.HandleAsync(stoppingToken);

                if (result.ChargedCount > 0 || result.InterruptedCount > 0)
                {
                    _logger.LogInformation(
                        "Session billing: {Charged} session(s) charged, {Interrupted} interrupted (insufficient funds).",
                        result.ChargedCount, result.InterruptedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session billing iteration failed.");
            }
        }
    }
}
