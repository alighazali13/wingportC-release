using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly CashierDbContext _context;

    public AuditLogRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog auditLog)
    {
        auditLog.Id = Guid.NewGuid();
        auditLog.CreatedAt = DateTime.UtcNow;
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int count)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
