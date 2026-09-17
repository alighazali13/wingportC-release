namespace GamePort.Cashier.Contracts.Requests;

public class SendDeviceCommandRequest
{
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
}
