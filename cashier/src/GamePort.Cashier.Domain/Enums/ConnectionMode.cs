namespace GamePort.Cashier.Domain.Enums;

/// <summary>
/// How a device connects to the cashier server.
/// Client = PC Client via SignalR; Manual = staff-managed (PS5).
/// </summary>
public enum ConnectionMode
{
    Client = 1,
    Manual = 2
}
