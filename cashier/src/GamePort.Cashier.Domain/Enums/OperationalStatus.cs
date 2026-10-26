namespace GamePort.Cashier.Domain.Enums;

/// <summary>
/// Device operational state (independent from connectivity and activation).
/// </summary>
public enum OperationalStatus
{
    Available = 1,
    InUse = 2,
    Maintenance = 3
}
