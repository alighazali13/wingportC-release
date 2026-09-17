namespace GamePort.Cashier.Contracts.Hub;

public class SessionRestorePayload
{
    public Guid SessionId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? PlannedEndTime { get; set; }
    public decimal PriceAtStart { get; set; }
    public long TotalPausedSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
}
