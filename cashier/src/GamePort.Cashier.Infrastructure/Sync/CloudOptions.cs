namespace GamePort.Cashier.Infrastructure.Sync;

public class CloudOptions
{
    public const string SectionName = "Cloud";

    public bool Enabled { get; set; } = true;

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public int SyncIntervalSeconds { get; set; } = 30;

    public int TimeoutSeconds { get; set; } = 15;
}
