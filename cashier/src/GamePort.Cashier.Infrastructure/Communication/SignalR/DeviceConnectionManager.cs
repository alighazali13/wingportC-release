using System.Collections.Concurrent;

namespace GamePort.Cashier.Infrastructure.Communication.SignalR;

public class DeviceConnectionManager : Application.Interfaces.IDeviceConnectionManager
{
    private readonly ConcurrentDictionary<Guid, string> _deviceToConnection = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToDevice = new();
    private readonly object _sync = new();

    public void AddOrUpdate(Guid deviceId, string connectionId)
    {
        lock (_sync)
        {
            if (_deviceToConnection.TryGetValue(deviceId, out var previous) && previous != connectionId)
            {
                _connectionToDevice.TryRemove(previous, out _);
            }

            _deviceToConnection[deviceId] = connectionId;
            _connectionToDevice[connectionId] = deviceId;
        }
    }

    public bool TryRemoveByDevice(Guid deviceId, out string? connectionId)
    {
        lock (_sync)
        {
            if (_deviceToConnection.TryRemove(deviceId, out var removed))
            {
                _connectionToDevice.TryRemove(removed, out _);
                connectionId = removed;
                return true;
            }

            connectionId = null;
            return false;
        }
    }

    public bool TryRemoveByConnection(string connectionId, out Guid deviceId)
    {
        lock (_sync)
        {
            if (_connectionToDevice.TryRemove(connectionId, out var removed))
            {
                _deviceToConnection.TryRemove(removed, out _);
                deviceId = removed;
                return true;
            }

            deviceId = Guid.Empty;
            return false;
        }
    }

    public bool IsConnected(Guid deviceId) => _deviceToConnection.ContainsKey(deviceId);

    public string? GetConnectionId(Guid deviceId)
        => _deviceToConnection.TryGetValue(deviceId, out var connectionId) ? connectionId : null;

    public IReadOnlyCollection<Guid> ConnectedDeviceIds => _deviceToConnection.Keys.ToArray();
}
