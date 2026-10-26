namespace GamePort.Cashier.Contracts.Responses;

public class DeviceResponse
{
    // V1 fields (unchanged)
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

    // V2 fields
    public string OperationalStatus { get; set; } = string.Empty;
    public string ConnectivityStatus { get; set; } = string.Empty;
    public string ConnectionMode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? MaintenanceNotes { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public Guid? PricingGroupId { get; set; }
    public string? PricingGroupName { get; set; }
    public string? ConsoleModel { get; set; }
    public int? ControllerCount { get; set; }
    public int? MaxPlayers { get; set; }
    public string Xmin { get; set; } = string.Empty;
}

public class RegisteredDeviceResponse : DeviceResponse
{
    public string ClientSecret { get; set; } = string.Empty;
}
