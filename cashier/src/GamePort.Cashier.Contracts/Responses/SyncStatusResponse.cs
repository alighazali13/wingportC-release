namespace GamePort.Cashier.Contracts.Responses;

public class SyncStatusResponse
{
    public bool CloudConfigured { get; set; }
    public long OutboxCursor { get; set; }
    public int PendingOutbox { get; set; }
    public int FailedOutbox { get; set; }
    public DateTime? LastOutboxSyncAt { get; set; }
    public long InboxCursor { get; set; }
    public int PendingInbox { get; set; }
    public DateTime? LastInboxSyncAt { get; set; }
}
