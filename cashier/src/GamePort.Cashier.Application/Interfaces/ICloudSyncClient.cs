namespace GamePort.Cashier.Application.Interfaces;

public record CloudSyncEvent(
    long Sequence,
    string EventType,
    string Payload,
    string IdempotencyKey,
    DateTime OccurredAt);

public record CloudChange(
    long Cursor,
    string ExternalId,
    string MessageType,
    string Payload);

public record CloudPushResult(bool Success, string? Error);

public record CloudPullResult(bool Success, long NextCursor, IReadOnlyList<CloudChange> Changes, string? Error);

public interface ICloudSyncClient
{
    bool IsConfigured { get; }

    Task<CloudPushResult> PushAsync(IReadOnlyList<CloudSyncEvent> events, CancellationToken cancellationToken = default);

    Task<CloudPullResult> PullAsync(long cursor, int batchSize, CancellationToken cancellationToken = default);
}
