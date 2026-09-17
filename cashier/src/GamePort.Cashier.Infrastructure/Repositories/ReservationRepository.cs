using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly CashierDbContext _context;

    public ReservationRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Reservation?> GetByIdAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Device)
            .OrderBy(r => r.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reservation>> GetByDateAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Device)
            .Where(r => r.StartTime >= dayStart && r.StartTime < dayEnd)
            .OrderBy(r => r.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reservation>> GetUpcomingAsync(DateTime from)
    {
        return await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Device)
            .Where(r => r.Status == ReservationStatus.Reserved && r.EndTime >= from)
            .OrderBy(r => r.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reservation>> GetOverlappingAsync(Guid deviceId, DateTime start, DateTime end)
    {
        return await _context.Reservations
            .Where(r => r.DeviceId == deviceId
                        && r.Status == ReservationStatus.Reserved
                        && r.StartTime < end
                        && r.EndTime > start)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reservation>> GetExpiredAsync(DateTime referenceTime)
    {
        return await _context.Reservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.EndTime < referenceTime)
            .ToListAsync();
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        reservation.Id = Guid.NewGuid();
        reservation.CreatedAt = DateTime.UtcNow;
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        reservation.UpdatedAt = DateTime.UtcNow;
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }
}
