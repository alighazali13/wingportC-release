namespace GamePort.Cashier.Application.Interfaces;

public record BackupResult(bool Success, string? FilePath, string? Error);

public record BackupInfo(string FileName, long SizeBytes, DateTime CreatedAt);

public interface IBackupService
{
    bool IsSupported { get; }

    Task<BackupResult> CreateBackupAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);
}
