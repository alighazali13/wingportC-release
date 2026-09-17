using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class Device : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public DeviceStatus Status { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? HardwareInfo { get; set; }
    public string? ClientIdentity { get; set; }
    public string? ClientSecretHash { get; set; }
    public DateTime? CredentialIssuedAt { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public Guid? CurrentGameId { get; set; }
}
