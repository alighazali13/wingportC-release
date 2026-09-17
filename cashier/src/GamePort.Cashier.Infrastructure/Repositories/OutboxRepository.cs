using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly CashierDbContext _context;

    public OutboxRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<OutboxEvent> AddAsync(OutboxEvent outboxEvent)
    {
        var lastSequence = await _context.OutboxEvents
            .OrderByDescending(e => e.Sequence)
            .Select(e => (long?)e.Sequence)
            .FirstOrDefaultAsync() ?? 0;

        outboxEvent.Id = Guid.NewGuid();
        outboxEvent.CreatedAt = DateTime.UtcNow;
        outboxEvent.Sequence = lastSequence + 1;
        outboxEvent.Status = OutboxStatus.Pending;

        _context.OutboxEvents.Add(outboxEvent);
        await _context.SaveChangesAsync();
        return outboxEvent;
    }

    public async Task<IEnumerable<OutboxEvent>> GetPendingAsync(int batchSize)
    {
        return await _context.OutboxEvents
            .Where(e => e.Status == OutboxStatus.Pending)
            .OrderBy(e => e.Sequence)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<OutboxEvent>> GetDueAsync(DateTime referenceTime, int batchSize)
    {
        return await _context.OutboxEvents
            .Where(e => e.Status == OutboxStatus.Pending
                        && (e.NextAttemptAt == null || e.NextAttemptAt <= referenceTime))
            .OrderBy(e => e.Sequence)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<int> CountByStatusAsync(OutboxStatus status)
    {
        return await _context.OutboxEvents.CountAsync(e => e.Status == status);
    }

    public async Task UpdateAsync(OutboxEvent outboxEvent)
    {
        outboxEvent.UpdatedAt = DateTime.UtcNow;
        _context.OutboxEvents.Update(outboxEvent);
        await _context.SaveChangesAsync();
    }
}
