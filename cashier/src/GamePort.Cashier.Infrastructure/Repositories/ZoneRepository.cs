using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class ZoneRepository : IZoneRepository
{
    private readonly CashierDbContext _context;

    public ZoneRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Zone?> GetByIdAsync(Guid id)
    {
        return await _context.Zones
            .FirstOrDefaultAsync(z => z.Id == id && !z.IsDeleted);
    }

    public async Task<IEnumerable<Zone>> GetAllAsync()
    {
        return await _context.Zones
            .Where(z => !z.IsDeleted)
            .OrderBy(z => z.DisplayOrder)
            .ThenBy(z => z.Name)
            .ToListAsync();
    }

    public async Task<Zone> CreateAsync(Zone zone)
    {
        zone.Id = zone.Id == Guid.Empty ? Guid.NewGuid() : zone.Id;
        zone.CreatedAt = DateTime.UtcNow;
        _context.Zones.Add(zone);
        await _context.SaveChangesAsync();
        return zone;
    }

    public async Task UpdateAsync(Zone zone)
    {
        zone.UpdatedAt = DateTime.UtcNow;
        _context.Zones.Update(zone);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var zone = await _context.Zones.FirstOrDefaultAsync(z => z.Id == id);
        if (zone != null)
        {
            zone.IsDeleted = true;
            zone.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasChildrenAsync(Guid zoneId)
    {
        return await _context.Zones.AnyAsync(z => z.ParentId == zoneId && !z.IsDeleted);
    }

    public async Task<bool> HasDevicesAsync(Guid zoneId)
    {
        return await _context.Devices.AnyAsync(d => d.ZoneId == zoneId && !d.IsDeleted);
    }
}
