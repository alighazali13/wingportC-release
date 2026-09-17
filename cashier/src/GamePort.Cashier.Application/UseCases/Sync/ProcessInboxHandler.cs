using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sync;

public record ProcessInboxResult(int Received, int Applied, int Skipped, int Failed, bool CloudUnavailable);

public class ProcessInboxHandler
{
    public const string CheckpointName = "inbox";

    private const int BatchSize = 100;

    private readonly IInboxRepository _inbox;
    private readonly ISyncCheckpointRepository _checkpoints;
    private readonly ICloudSyncClient _cloud;
    private readonly CloudMessageDispatcher _dispatcher;

    public ProcessInboxHandler(
        IInboxRepository inbox,
        ISyncCheckpointRepository checkpoints,
        ICloudSyncClient cloud,
        CloudMessageDispatcher dispatcher)
    {
        _inbox = inbox;
        _checkpoints = checkpoints;
        _cloud = cloud;
        _dispatcher = dispatcher;
    }

    public async Task<ProcessInboxResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!_cloud.IsConfigured)
        {
            return new ProcessInboxResult(0, 0, 0, 0, true);
        }

        var checkpoint = await _checkpoints.GetByNameAsync(CheckpointName);
        var cursor = checkpoint?.Cursor ?? 0;

        var pull = await _cloud.PullAsync(cursor, BatchSize, cancellationToken);
        if (!pull.Success)
        {
            return new ProcessInboxResult(0, 0, 0, 0, true);
        }

        var applied = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var change in pull.Changes)
        {
            if (await _inbox.ExistsAsync(change.ExternalId))
            {
                skipped++;
                continue;
            }

            var message = await _inbox.AddAsync(new InboxMessage
            {
                ExternalId = change.ExternalId,
                MessageType = change.MessageType,
                Payload = change.Payload,
                Status = InboxStatus.Pending
            });

            var handled = await _dispatcher.DispatchAsync(change.MessageType, change.Payload, cancellationToken);

            message.Status = handled ? InboxStatus.Processed : InboxStatus.Failed;
            message.ProcessedAt = DateTime.UtcNow;
            message.LastError = handled ? null : $"No handler produced a result for '{change.MessageType}'.";
            await _inbox.UpdateAsync(message);

            if (handled)
            {
                applied++;
            }
            else
            {
                failed++;
            }
        }

        await _checkpoints.UpsertAsync(CheckpointName, pull.NextCursor, DateTime.UtcNow);

        return new ProcessInboxResult(pull.Changes.Count, applied, skipped, failed, false);
    }
}
