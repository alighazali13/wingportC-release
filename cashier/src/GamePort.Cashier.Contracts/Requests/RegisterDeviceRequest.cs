namespace GamePort.Cashier.Contracts.Requests;

public class RegisterDeviceRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? HardwareInfo { get; set; }
}
