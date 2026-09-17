namespace GamePort.Cashier.Contracts.Responses;

public class GameResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ExecutablePath { get; set; }
    public string? IconPath { get; set; }
    public bool IsActive { get; set; }
    public string SupportedDeviceType { get; set; } = string.Empty;
    public string? LaunchConfiguration { get; set; }
}

public class DeviceGameResponse
{
    public Guid DeviceId { get; set; }
    public Guid GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string? InstalledVersion { get; set; }
    public bool IsActive { get; set; }
}
