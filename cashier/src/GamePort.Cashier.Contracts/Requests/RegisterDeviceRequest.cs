namespace GamePort.Cashier.Contracts.Requests;

public class RegisterDeviceRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    // Required for GamingPC; optional for PlayStation
    public string? IpAddress { get; set; }

    public string? MacAddress { get; set; }
    public string? HardwareInfo { get; set; }

    // V2 optional fields
    public Guid? ZoneId { get; set; }
    public Guid? PricingGroupId { get; set; }
    public int DisplayOrder { get; set; }

    // PS5-specific (ignored for GamingPC)
    public string? ConsoleModel { get; set; }
    public int? ControllerCount { get; set; }
    public int? MaxPlayers { get; set; }
}
