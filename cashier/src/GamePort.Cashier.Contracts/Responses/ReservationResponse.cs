namespace GamePort.Cashier.Contracts.Responses;

public class ReservationResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedSessionId { get; set; }
}
