namespace GamePort.Cashier.Contracts.Requests;

public class EndSessionRequest
{
    public Guid SessionId { get; set; }
    public string? Reason { get; set; }
}
