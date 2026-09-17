using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class DeviceGame : BaseEntity
{
    public Guid DeviceId { get; set; }
    public Guid GameId { get; set; }
    public bool IsInstalled { get; set; }
    public string? InstalledVersion { get; set; }
    public DateTime? InstalledAt { get; set; }
    public Device Device { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
