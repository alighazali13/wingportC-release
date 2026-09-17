using GamePort.Cashier.Contracts.Hub;

namespace GamePort.Cashier.Application.Interfaces;

public interface IDeviceCommandSender
{
    Task<bool> SendAsync(Guid deviceId, DeviceCommandMessage command, CancellationToken cancellationToken = default);
}
