using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class DeviceHeartbeatMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CashierServerOptions> _options;
    private readonly ILogger<DeviceHeartbeatMonitor> _logger;

    public DeviceHeartbeatMonitor(
        IServiceScopeFactory scopeFactory,
        IOptions<CashierServerOptions> options,
        ILogger<DeviceHeartbeatMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeoutSeconds = Math.Max(15, _options.Value.HeartbeatTimeoutSeconds);
        var interval = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds / 3));

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
                await CheckDevicesAsync(timeoutSeconds, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device heartbeat monitor iteration failed.");
            }
        }
    }

    private async Task CheckDevicesAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

        var threshold = DateTime.UtcNow.AddSeconds(-timeoutSeconds);
        var all = await devices.GetAllAsync();

        foreach (var device in all)
        {
            if (!device.IsConnected)
            {
                continue;
            }

            if (device.LastHeartbeat is null || device.LastHeartbeat < threshold)
            {
                device.IsConnected = false;
                if (device.Status != DeviceStatus.Maintenance)
                {
                    device.Status = DeviceStatus.Disconnected;
                }

                await devices.UpdateAsync(device);
                _logger.LogWarning("Device {DeviceId} marked disconnected due to heartbeat timeout.", device.Id);
            }
        }
    }
}
