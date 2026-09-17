using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Contracts.Hub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Communication.SignalR;

public class SignalRDeviceCommandSender : IDeviceCommandSender
{
    private readonly IHubContext<ClientHub, IClientHubClient> _hubContext;
    private readonly IDeviceConnectionManager _connections;
    private readonly ILogger<SignalRDeviceCommandSender> _logger;

    public SignalRDeviceCommandSender(
        IHubContext<ClientHub, IClientHubClient> hubContext,
        IDeviceConnectionManager connections,
        ILogger<SignalRDeviceCommandSender> logger)
    {
        _hubContext = hubContext;
        _connections = connections;
        _logger = logger;
    }

    public async Task<bool> SendAsync(Guid deviceId, DeviceCommandMessage command, CancellationToken cancellationToken = default)
    {
        var connectionId = _connections.GetConnectionId(deviceId);
        if (connectionId is null)
        {
            _logger.LogWarning("Cannot send command {CommandType} to device {DeviceId}: device is not connected.", command.CommandType, deviceId);
            return false;
        }

        command.DeviceId = deviceId;
        await _hubContext.Clients.Client(connectionId).ReceiveCommand(command);
        return true;
    }
}
