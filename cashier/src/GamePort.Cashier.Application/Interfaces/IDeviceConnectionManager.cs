namespace GamePort.Cashier.Application.Interfaces;

public interface IDeviceConnectionManager
{
    void AddOrUpdate(Guid deviceId, string connectionId);
    bool TryRemoveByDevice(Guid deviceId, out string? connectionId);
    bool TryRemoveByConnection(string connectionId, out Guid deviceId);
    bool IsConnected(Guid deviceId);
    string? GetConnectionId(Guid deviceId);
    IReadOnlyCollection<Guid> ConnectedDeviceIds { get; }
}
