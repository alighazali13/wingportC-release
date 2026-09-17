namespace GamePort.Cashier.Contracts.Requests;

public class CancelSessionRequest
{
    public Guid SessionId { get; set; }
    public string? Reason { get; set; }
}
