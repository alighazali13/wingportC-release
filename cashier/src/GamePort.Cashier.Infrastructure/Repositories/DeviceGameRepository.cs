using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class DeviceGameRepository : IDeviceGameRepository
{
    private readonly CashierDbContext _context;

    public DeviceGameRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceGame?> GetAsync(Guid deviceId, Guid gameId)
    {
        return await _context.DeviceGames
            .FirstOrDefaultAsync(dg => dg.DeviceId == deviceId && dg.GameId == gameId);
    }

    public async Task<IEnumerable<DeviceGame>> GetByDeviceAsync(Guid deviceId)
    {
        return await _context.DeviceGames
            .Include(dg => dg.Game)
            .Where(dg => dg.DeviceId == deviceId)
            .ToListAsync();
    }

    public async Task<DeviceGame> AddAsync(DeviceGame deviceGame)
    {
        deviceGame.Id = Guid.NewGuid();
        deviceGame.CreatedAt = DateTime.UtcNow;
        _context.DeviceGames.Add(deviceGame);
        await _context.SaveChangesAsync();
        return deviceGame;
    }

    public async Task UpdateAsync(DeviceGame deviceGame)
    {
        deviceGame.UpdatedAt = DateTime.UtcNow;
        _context.DeviceGames.Update(deviceGame);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid deviceId, Guid gameId)
    {
        var deviceGame = await _context.DeviceGames
            .FirstOrDefaultAsync(dg => dg.DeviceId == deviceId && dg.GameId == gameId);

        if (deviceGame is not null)
        {
            _context.DeviceGames.Remove(deviceGame);
            await _context.SaveChangesAsync();
        }
    }
}
