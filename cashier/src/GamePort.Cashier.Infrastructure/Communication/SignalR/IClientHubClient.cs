using GamePort.Cashier.Contracts.Hub;

namespace GamePort.Cashier.Infrastructure.Communication.SignalR;

public interface IClientHubClient
{
    Task ReceiveCommand(DeviceCommandMessage command);
}
