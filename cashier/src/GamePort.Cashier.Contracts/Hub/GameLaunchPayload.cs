namespace GamePort.Cashier.Contracts.Hub;

public class GameLaunchPayload
{
    public Guid GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? LaunchConfiguration { get; set; }
}
