namespace GamePort.Cashier.Infrastructure.Backup;

public class BackupOptions
{
    public const string SectionName = "Backup";

    public bool Enabled { get; set; } = true;

    public string Directory { get; set; } = "Backups";

    public int IntervalHours { get; set; } = 24;

    public int RetainCount { get; set; } = 14;

    public string PgDumpPath { get; set; } = "pg_dump";
}
