using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sync;

public record SyncStatusResult(
    bool CloudConfigured,
    long OutboxCursor,
    int PendingOutbox,
    int FailedOutbox,
    DateTime? LastOutboxSyncAt,
    long InboxCursor,
    int PendingInbox,
    DateTime? LastInboxSyncAt);

public class GetSyncStatusHandler
{
    private readonly IOutboxRepository _outbox;
    private readonly IInboxRepository _inbox;
    private readonly ISyncCheckpointRepository _checkpoints;
    private readonly ICloudSyncClient _cloud;

    public GetSyncStatusHandler(
        IOutboxRepository outbox,
        IInboxRepository inbox,
        ISyncCheckpointRepository checkpoints,
        ICloudSyncClient cloud)
    {
        _outbox = outbox;
        _inbox = inbox;
        _checkpoints = checkpoints;
        _cloud = cloud;
    }

    public async Task<SyncStatusResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var outboxCheckpoint = await _checkpoints.GetByNameAsync(ProcessOutboxHandler.CheckpointName);
        var inboxCheckpoint = await _checkpoints.GetByNameAsync(ProcessInboxHandler.CheckpointName);

        return new SyncStatusResult(
            _cloud.IsConfigured,
            outboxCheckpoint?.Cursor ?? 0,
            await _outbox.CountByStatusAsync(OutboxStatus.Pending),
            await _outbox.CountByStatusAsync(OutboxStatus.Failed),
            outboxCheckpoint?.LastSyncedAt,
            inboxCheckpoint?.Cursor ?? 0,
            await _inbox.CountByStatusAsync(InboxStatus.Pending),
            inboxCheckpoint?.LastSyncedAt);
    }
}
