using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class SyncCheckpoint : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public long Cursor { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
