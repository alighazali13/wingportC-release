namespace GamePort.Cashier.Application.Interfaces;

public interface ICloudMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
