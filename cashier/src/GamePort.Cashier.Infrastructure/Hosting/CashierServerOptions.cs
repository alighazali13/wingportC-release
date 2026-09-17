namespace GamePort.Cashier.Infrastructure.Hosting;

public class CashierServerOptions
{
    public const string SectionName = "Server";

    public string[] Urls { get; set; } = { "http://0.0.0.0:5210" };

    public string? ApiKey { get; set; }

    public int HeartbeatTimeoutSeconds { get; set; } = 60;

    public int BillingIntervalSeconds { get; set; } = 60;
}
