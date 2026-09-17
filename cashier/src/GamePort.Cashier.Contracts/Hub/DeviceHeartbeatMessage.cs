namespace GamePort.Cashier.Contracts.Hub;

public class DeviceHeartbeatMessage
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? CurrentGame { get; set; }
    public bool IsLocked { get; set; }
    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
}
