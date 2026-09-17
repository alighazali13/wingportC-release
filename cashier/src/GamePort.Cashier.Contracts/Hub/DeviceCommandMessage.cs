namespace GamePort.Cashier.Contracts.Hub;

public enum DeviceCommandType
{
    RefreshState = 0,
    Lock = 1,
    Unlock = 2,
    Restart = 3,
    Shutdown = 4,
    LaunchGame = 5,
    CloseGame = 6,
    SetVolume = 7,
    SetMouseSpeed = 8,
    SetBackground = 9,
    RestoreSession = 10
}

public class DeviceCommandMessage
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public DeviceCommandType CommandType { get; set; }
    public string? Payload { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
