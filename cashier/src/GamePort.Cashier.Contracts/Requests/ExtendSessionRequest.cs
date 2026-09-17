namespace GamePort.Cashier.Contracts.Requests;

public class ExtendSessionRequest
{
    public Guid SessionId { get; set; }
    public DateTime? NewPlannedEndTime { get; set; }
    public int? AdditionalMinutes { get; set; }
}
