using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class SyncCheckpointRepository : ISyncCheckpointRepository
{
    private readonly CashierDbContext _context;

    public SyncCheckpointRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<SyncCheckpoint?> GetByNameAsync(string name)
    {
        return await _context.SyncCheckpoints.FirstOrDefaultAsync(c => c.Name == name);
    }

    public async Task<SyncCheckpoint> UpsertAsync(string name, long cursor, DateTime? lastSyncedAt)
    {
        var checkpoint = await _context.SyncCheckpoints.FirstOrDefaultAsync(c => c.Name == name);
        if (checkpoint is null)
        {
            checkpoint = new SyncCheckpoint
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                Name = name,
                Cursor = cursor,
                LastSyncedAt = lastSyncedAt
            };
            _context.SyncCheckpoints.Add(checkpoint);
        }
        else
        {
            checkpoint.Cursor = cursor;
            checkpoint.LastSyncedAt = lastSyncedAt;
            checkpoint.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return checkpoint;
    }
}
