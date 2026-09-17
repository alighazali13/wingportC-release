using GamePort.Cashier.Application.Interfaces;

namespace GamePort.Cashier.Application.UseCases.Sync;

public class CloudMessageDispatcher
{
    private readonly IReadOnlyDictionary<string, ICloudMessageHandler> _handlers;

    public CloudMessageDispatcher(IEnumerable<ICloudMessageHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.MessageType, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> DispatchAsync(string messageType, string payload, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(messageType, out var handler))
        {
            return false;
        }

        await handler.HandleAsync(payload, cancellationToken);
        return true;
    }
}
