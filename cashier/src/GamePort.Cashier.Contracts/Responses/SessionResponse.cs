namespace GamePort.Cashier.Contracts.Responses;

public class SessionResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? PlannedEndTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public DateTime? PausedAt { get; set; }
    public long TotalPausedSeconds { get; set; }
    public decimal PriceAtStart { get; set; }
    public string Status { get; set; } = string.Empty;
}
