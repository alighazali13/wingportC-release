namespace GamePort.Cashier.Contracts.Responses;

public class DeviceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? ClientIdentity { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public Guid? CurrentGameId { get; set; }
}

public class RegisteredDeviceResponse : DeviceResponse
{
    public string ClientSecret { get; set; } = string.Empty;
}
