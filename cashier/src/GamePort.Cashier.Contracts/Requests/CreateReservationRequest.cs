namespace GamePort.Cashier.Contracts.Requests;

public class CreateReservationRequest
{
    public Guid CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Notes { get; set; }
}
