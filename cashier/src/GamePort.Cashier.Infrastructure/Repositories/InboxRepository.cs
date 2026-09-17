using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class InboxRepository : IInboxRepository
{
    private readonly CashierDbContext _context;

    public InboxRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string externalId)
    {
        return await _context.InboxMessages.AnyAsync(m => m.ExternalId == externalId);
    }

    public async Task<InboxMessage> AddAsync(InboxMessage message)
    {
        message.Id = Guid.NewGuid();
        message.CreatedAt = DateTime.UtcNow;
        _context.InboxMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<int> CountByStatusAsync(InboxStatus status)
    {
        return await _context.InboxMessages.CountAsync(m => m.Status == status);
    }

    public async Task UpdateAsync(InboxMessage message)
    {
        message.UpdatedAt = DateTime.UtcNow;
        _context.InboxMessages.Update(message);
        await _context.SaveChangesAsync();
    }
}
