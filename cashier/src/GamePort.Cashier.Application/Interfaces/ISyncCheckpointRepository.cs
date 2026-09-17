using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface ISyncCheckpointRepository
{
    Task<SyncCheckpoint?> GetByNameAsync(string name);
    Task<SyncCheckpoint> UpsertAsync(string name, long cursor, DateTime? lastSyncedAt);
}
