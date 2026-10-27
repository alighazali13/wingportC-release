using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Domain.Exceptions;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly CashierDbContext _context;

    public DeviceRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetByIdAsync(Guid id)
    {
        return await _context.Devices
            .Include(d => d.Zone)
            .Include(d => d.PricingGroup)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
    }

    public async Task<Device?> GetByClientIdentityAsync(string clientIdentity)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.ClientIdentity == clientIdentity && !d.IsDeleted);
    }

    public async Task<IEnumerable<Device>> GetAllAsync()
    {
        return await _context.Devices
            .Include(d => d.Zone)
            .Include(d => d.PricingGroup)
            .Where(d => !d.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Device>> GetByTypeAsync(DeviceType type)
    {
        return await _context.Devices
            .Where(d => d.Type == type && !d.IsDeleted)
            .ToListAsync();
    }

    public async Task<Device> CreateAsync(Device device)
    {
        device.Id = Guid.NewGuid();
        device.CreatedAt = DateTime.UtcNow;
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task UpdateAsync(Device device)
    {
        device.UpdatedAt = DateTime.UtcNow;

        // Only attach when detached. For an already-tracked entity, calling Update()
        // would mark every property modified and rewrite unchanged columns; more
        // importantly we must preserve the xmin OriginalValue captured at load time
        // so SaveChanges enforces PostgreSQL optimistic concurrency.
        if (_context.Entry(device).State == EntityState.Detached)
        {
            _context.Devices.Update(device);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "نسخه ارسال‌شده منسوخ شده است؛ دستگاه توسط کاربر دیگری تغییر کرده است. لطفاً دوباره بارگذاری کنید.",
                ex);
        }
    }
}
