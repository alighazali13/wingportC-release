namespace GamePort.Cashier.Contracts.Requests;

public class OperationalStatusRequest
{
    // Only "Available" or "Maintenance" — InUse is session-lifecycle only
    public string OperationalStatus { get; set; } = string.Empty;
    public string? MaintenanceNotes { get; set; }
    public string Xmin { get; set; } = string.Empty;
}
