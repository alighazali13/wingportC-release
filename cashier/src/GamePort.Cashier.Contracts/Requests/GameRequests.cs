namespace GamePort.Cashier.Contracts.Requests;

public class CreateGameRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ExecutablePath { get; set; }
    public string? IconPath { get; set; }
    public string SupportedDeviceType { get; set; } = "GamingPC";
    public string? LaunchConfiguration { get; set; }
}

public class UpdateGameRequest
{
    public Guid GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ExecutablePath { get; set; }
    public string? IconPath { get; set; }
    public string SupportedDeviceType { get; set; } = "GamingPC";
    public string? LaunchConfiguration { get; set; }
    public bool IsActive { get; set; } = true;
}

public class GameIdRequest
{
    public Guid GameId { get; set; }
}

public class AssignGameToDeviceRequest
{
    public Guid DeviceId { get; set; }
    public Guid GameId { get; set; }
    public bool IsInstalled { get; set; } = true;
    public string? InstalledVersion { get; set; }
}

public class LaunchGameRequest
{
    public Guid DeviceId { get; set; }
    public Guid GameId { get; set; }
}

public class DeviceIdRequest
{
    public Guid DeviceId { get; set; }
}
