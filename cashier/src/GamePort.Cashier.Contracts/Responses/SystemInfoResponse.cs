namespace GamePort.Cashier.Contracts.Responses;

public class SystemInfoResponse
{
    public string Version { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<string> ServerUrls { get; set; } = new();
    public string DatabaseProvider { get; set; } = string.Empty;
    public bool AutoMigrate { get; set; }
    public bool CloudConfigured { get; set; }
    public string? CloudBaseUrl { get; set; }
    public bool BackupSupported { get; set; }
    public bool ApiKeyConfigured { get; set; }
    public DateTime ServerTime { get; set; }
}
