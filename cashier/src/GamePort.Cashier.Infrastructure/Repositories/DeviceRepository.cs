using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
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
        return await _context.Devices.FindAsync(id);
    }

    public async Task<Device?> GetByClientIdentityAsync(string clientIdentity)
    {
        return await _context.Devices.FirstOrDefaultAsync(d => d.ClientIdentity == clientIdentity);
    }

    public async Task<IEnumerable<Device>> GetAllAsync()
    {
        return await _context.Devices.ToListAsync();
    }

    public async Task<IEnumerable<Device>> GetByTypeAsync(DeviceType type)
    {
        return await _context.Devices.Where(d => d.Type == type).ToListAsync();
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
        _context.Devices.Update(device);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device != null)
        {
            device.IsDeleted = true;
            device.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
