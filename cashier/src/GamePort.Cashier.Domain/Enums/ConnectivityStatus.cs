namespace GamePort.Cashier.Domain.Enums;

/// <summary>
/// Device connectivity state (independent from operational status).
/// </summary>
public enum ConnectivityStatus
{
    Connected = 1,
    Disconnected = 2,
    NotApplicable = 3
}
