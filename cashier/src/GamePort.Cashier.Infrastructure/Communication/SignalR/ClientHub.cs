using System.Security.Claims;
using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Contracts.Hub;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Communication.SignalR;

[Authorize(AuthenticationSchemes = DeviceAuthenticationDefaults.Scheme)]
public class ClientHub : Hub<IClientHubClient>
{
    private readonly IDeviceConnectionManager _connections;
    private readonly IDeviceRepository _devices;
    private readonly ISessionRepository _sessions;
    private readonly ILogger<ClientHub> _logger;

    public ClientHub(
        IDeviceConnectionManager connections,
        IDeviceRepository devices,
        ISessionRepository sessions,
        ILogger<ClientHub> logger)
    {
        _connections = connections;
        _devices = devices;
        _sessions = sessions;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var deviceId = GetDeviceId();
        if (deviceId is null)
        {
            _logger.LogWarning("Client connected without a valid device identity claim.");
            Context.Abort();
            return;
        }

        _connections.AddOrUpdate(deviceId.Value, Context.ConnectionId);

        var device = await _devices.GetByIdAsync(deviceId.Value);
        if (device is not null)
        {
            device.IsConnected = true;
            device.LastHeartbeat = DateTime.UtcNow;
            if (device.Status == DeviceStatus.Disconnected || device.Status == DeviceStatus.Offline)
            {
                device.Status = DeviceStatus.Available;
            }

            await _devices.UpdateAsync(device);
        }

        await RestoreActiveSessionAsync(deviceId.Value);

        _logger.LogInformation("Device {DeviceId} connected (connection {ConnectionId}).", deviceId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemoveByConnection(Context.ConnectionId, out var deviceId))
        {
            var device = await _devices.GetByIdAsync(deviceId);
            if (device is not null)
            {
                device.IsConnected = false;
                if (device.Status != DeviceStatus.Maintenance)
                {
                    device.Status = DeviceStatus.Disconnected;
                }

                await _devices.UpdateAsync(device);
            }

            _logger.LogInformation("Device {DeviceId} disconnected (connection {ConnectionId}).", deviceId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Heartbeat(DeviceHeartbeatMessage message)
    {
        var deviceId = GetDeviceId();
        if (deviceId is null)
        {
            return;
        }

        var device = await _devices.GetByIdAsync(deviceId.Value);
        if (device is null)
        {
            return;
        }

        device.IsConnected = true;
        device.LastHeartbeat = DateTime.UtcNow;
        await _devices.UpdateAsync(device);
    }

    public Task AcknowledgeCommand(Guid commandId, bool success, string? error)
    {
        _logger.LogInformation(
            "Command {CommandId} acknowledged by device {DeviceId}: success={Success}, error={Error}.",
            commandId, GetDeviceId(), success, error);
        return Task.CompletedTask;
    }

    public async Task ReportGameState(Guid? gameId)
    {
        var deviceId = GetDeviceId();
        if (deviceId is null)
        {
            return;
        }

        var device = await _devices.GetByIdAsync(deviceId.Value);
        if (device is null)
        {
            return;
        }

        device.CurrentGameId = gameId;
        await _devices.UpdateAsync(device);

        _logger.LogInformation("Device {DeviceId} reported running game {GameId}.", deviceId, gameId);
    }

    private async Task RestoreActiveSessionAsync(Guid deviceId)
    {
        var session = await _sessions.GetOpenByDeviceIdAsync(deviceId);
        if (session is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new SessionRestorePayload
        {
            SessionId = session.Id,
            CustomerId = session.CustomerId,
            CustomerName = session.Customer?.Name ?? string.Empty,
            DeviceId = deviceId,
            StartTime = session.StartTime,
            PlannedEndTime = session.PlannedEndTime,
            PriceAtStart = session.PriceAtStart,
            TotalPausedSeconds = session.TotalPausedSeconds,
            Status = session.Status.ToString()
        });

        await Clients.Caller.ReceiveCommand(new DeviceCommandMessage
        {
            DeviceId = deviceId,
            CommandType = DeviceCommandType.RestoreSession,
            Payload = payload
        });

        _logger.LogInformation("Restored active session {SessionId} to device {DeviceId}.", session.Id, deviceId);
    }

    private Guid? GetDeviceId()
    {
        var value = Context.User?.FindFirst(DeviceAuthenticationDefaults.DeviceIdClaim)?.Value;
        return Guid.TryParse(value, out var deviceId) ? deviceId : null;
    }
}
