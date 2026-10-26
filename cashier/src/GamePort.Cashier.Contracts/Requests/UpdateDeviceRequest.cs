namespace GamePort.Cashier.Contracts.Requests;

public class UpdateDeviceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid? PricingGroupId { get; set; }
    public int DisplayOrder { get; set; }
    public string? MaintenanceNotes { get; set; }

    // PS5-specific
    public string? ConsoleModel { get; set; }
    public int? ControllerCount { get; set; }
    public int? MaxPlayers { get; set; }

    // Required for optimistic concurrency (xmin as string)
    public string Xmin { get; set; } = string.Empty;
}
