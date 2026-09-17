using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly CashierDbContext _context;

    public AttendanceRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<AttendanceRecord?> GetOpenByEmployeeAsync(Guid employeeId)
    {
        return await _context.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.ClockOutAt == null)
            .OrderByDescending(a => a.ClockInAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AttendanceRecord>> GetByEmployeeAsync(Guid employeeId, int limit)
    {
        return await _context.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.ClockInAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<AttendanceRecord>> GetByDateAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await _context.AttendanceRecords
            .Include(a => a.Employee)
            .Where(a => a.ClockInAt >= dayStart && a.ClockInAt < dayEnd)
            .OrderBy(a => a.ClockInAt)
            .ToListAsync();
    }

    public async Task<AttendanceRecord> CreateAsync(AttendanceRecord record)
    {
        record.Id = Guid.NewGuid();
        record.CreatedAt = DateTime.UtcNow;
        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task UpdateAsync(AttendanceRecord record)
    {
        record.UpdatedAt = DateTime.UtcNow;
        _context.AttendanceRecords.Update(record);
        await _context.SaveChangesAsync();
    }
}
