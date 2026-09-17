using GamePort.Cashier.Application.UseCases.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class SessionExpirationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionExpirationService> _logger;

    public SessionExpirationService(IServiceScopeFactory scopeFactory, ILogger<SessionExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ExpireSessionsHandler>();
                var result = await handler.HandleAsync(stoppingToken);

                if (result.ExpiredCount > 0)
                {
                    _logger.LogInformation("Session expiration: {Count} session(s) expired.", result.ExpiredCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session expiration iteration failed.");
            }
        }
    }
}
