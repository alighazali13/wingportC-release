namespace GamePort.Cashier.Services.Updates;

public enum UpdateState
{
    Idle,
    Checking,
    Downloading,
    Ready,
    Failed
}

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "unknown";
    public string? AvailableVersion { get; set; }
    public UpdateState State { get; set; } = UpdateState.Idle;
    public string? ErrorMessage { get; set; }
    public int DownloadProgress { get; set; }
}
