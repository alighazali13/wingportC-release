using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sync;

public record ProcessOutboxResult(int Pushed, int Failed, bool CloudUnavailable);

public class ProcessOutboxHandler
{
    public const string CheckpointName = "outbox";

    private const int BatchSize = 100;
    private const int MaxAttempts = 10;

    private readonly IOutboxRepository _outbox;
    private readonly ISyncCheckpointRepository _checkpoints;
    private readonly ICloudSyncClient _cloud;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessOutboxHandler(
        IOutboxRepository outbox,
        ISyncCheckpointRepository checkpoints,
        ICloudSyncClient cloud,
        IUnitOfWork unitOfWork)
    {
        _outbox = outbox;
        _checkpoints = checkpoints;
        _cloud = cloud;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProcessOutboxResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!_cloud.IsConfigured)
        {
            return new ProcessOutboxResult(0, 0, true);
        }

        var now = DateTime.UtcNow;
        var due = (await _outbox.GetDueAsync(now, BatchSize)).ToList();
        if (due.Count == 0)
        {
            return new ProcessOutboxResult(0, 0, false);
        }

        var events = due
            .Select(e => new CloudSyncEvent(e.Sequence, e.EventType, e.Payload, e.IdempotencyKey, e.CreatedAt))
            .ToList();

        var pushResult = await _cloud.PushAsync(events, cancellationToken);

        if (pushResult.Success)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                foreach (var outboxEvent in due)
                {
                    outboxEvent.Status = OutboxStatus.Completed;
                    outboxEvent.ProcessedAt = DateTime.UtcNow;
                    outboxEvent.LastError = null;
                    await _outbox.UpdateAsync(outboxEvent);
                }
            }, cancellationToken);

            var maxSequence = due.Max(e => e.Sequence);
            await _checkpoints.UpsertAsync(CheckpointName, maxSequence, DateTime.UtcNow);

            return new ProcessOutboxResult(due.Count, 0, false);
        }

        var permanentlyFailed = 0;
        foreach (var outboxEvent in due)
        {
            outboxEvent.Attempts++;
            outboxEvent.LastError = pushResult.Error;

            if (outboxEvent.Attempts >= MaxAttempts)
            {
                outboxEvent.Status = OutboxStatus.Failed;
                permanentlyFailed++;
            }
            else
            {
                outboxEvent.Status = OutboxStatus.Pending;
                outboxEvent.NextAttemptAt = now.AddSeconds(BackoffSeconds(outboxEvent.Attempts));
            }

            await _outbox.UpdateAsync(outboxEvent);
        }

        return new ProcessOutboxResult(0, permanentlyFailed, true);
    }

    private static int BackoffSeconds(int attempts)
        => Math.Min(300, (int)Math.Pow(2, Math.Min(attempts, 8)));
}
