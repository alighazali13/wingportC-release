using GamePort.Cashier.Application.UseCases.Reservations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class ReservationExpirationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpirationService> _logger;

    public ReservationExpirationService(IServiceScopeFactory scopeFactory, ILogger<ReservationExpirationService> logger)
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
                var handler = scope.ServiceProvider.GetRequiredService<ExpireReservationsHandler>();
                var result = await handler.HandleAsync(stoppingToken);

                if (result.ExpiredCount > 0)
                {
                    _logger.LogInformation("Reservation expiration: {Count} reservation(s) expired.", result.ExpiredCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservation expiration iteration failed.");
            }
        }
    }
}
