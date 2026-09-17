using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class SessionRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionRecoveryService> _logger;

    public SessionRecoveryService(IServiceScopeFactory scopeFactory, ILogger<SessionRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

            var openSessions = (await sessions.GetOpenSessionsAsync()).ToList();
            var allDevices = (await devices.GetAllAsync()).ToList();
            var openDeviceIds = openSessions.Select(s => s.DeviceId).ToHashSet();

            var repaired = 0;

            foreach (var session in openSessions)
            {
                var device = allDevices.FirstOrDefault(d => d.Id == session.DeviceId);
                if (device is not null && device.Status is not (DeviceStatus.InUse or DeviceStatus.Maintenance))
                {
                    device.Status = DeviceStatus.InUse;
                    await devices.UpdateAsync(device);
                    repaired++;
                }
            }

            foreach (var device in allDevices)
            {
                if (device.Status == DeviceStatus.InUse && !openDeviceIds.Contains(device.Id))
                {
                    device.Status = DeviceStatus.Available;
                    await devices.UpdateAsync(device);
                    repaired++;
                }
            }

            _logger.LogInformation(
                "Session recovery completed: {OpenCount} open session(s), {RepairedCount} device state(s) repaired.",
                openSessions.Count, repaired);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session recovery failed.");
        }
    }
}
