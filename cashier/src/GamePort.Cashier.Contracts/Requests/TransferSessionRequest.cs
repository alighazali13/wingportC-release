namespace GamePort.Cashier.Contracts.Requests;

public class TransferSessionRequest
{
    public Guid SessionId { get; set; }
    public Guid TargetDeviceId { get; set; }
}
