using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly CashierDbContext _context;

    public SessionRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Session?> GetByIdAsync(Guid id)
    {
        return await _context.Sessions
            .Include(s => s.Customer)
            .Include(s => s.Device)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Session>> GetAllAsync()
    {
        return await _context.Sessions
            .Include(s => s.Customer)
            .Include(s => s.Device)
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetActiveSessionsAsync()
    {
        return await _context.Sessions
            .Include(s => s.Customer)
            .Include(s => s.Device)
            .Where(s => s.Status == SessionStatus.Active)
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetOpenSessionsAsync()
    {
        return await _context.Sessions
            .Include(s => s.Customer)
            .Include(s => s.Device)
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused)
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetByCustomerIdAsync(Guid customerId)
    {
        return await _context.Sessions
            .Include(s => s.Device)
            .Where(s => s.CustomerId == customerId)
            .ToListAsync();
    }

    public async Task<Session?> GetOpenByDeviceIdAsync(Guid deviceId)
    {
        return await _context.Sessions
            .Include(s => s.Customer)
            .Include(s => s.Device)
            .Where(s => s.DeviceId == deviceId
                        && (s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused))
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Session>> GetExpiredAsync(DateTime referenceTime)
    {
        return await _context.Sessions
            .Where(s => (s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused)
                        && s.PlannedEndTime != null
                        && s.PlannedEndTime < referenceTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        return await _context.Sessions
            .Where(s => idList.Contains(s.Id))
            .ToListAsync();
    }

    public async Task<Session> CreateAsync(Session session)
    {
        session.Id = Guid.NewGuid();
        session.CreatedAt = DateTime.UtcNow;
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task UpdateAsync(Session session)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync();
    }
}
