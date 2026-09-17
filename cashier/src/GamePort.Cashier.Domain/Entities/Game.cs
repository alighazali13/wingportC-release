using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class Game : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ExecutablePath { get; set; }
    public string? IconPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DeviceType SupportedDeviceType { get; set; }
    public string? LaunchConfiguration { get; set; }
}
