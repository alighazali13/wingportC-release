namespace GamePort.Cashier.Contracts.Requests;

public class StartSessionRequest
{
    public Guid CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime? PlannedEndTime { get; set; }
}
