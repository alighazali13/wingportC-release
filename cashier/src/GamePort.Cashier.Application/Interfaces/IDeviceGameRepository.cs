using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IDeviceGameRepository
{
    Task<DeviceGame?> GetAsync(Guid deviceId, Guid gameId);
    Task<IEnumerable<DeviceGame>> GetByDeviceAsync(Guid deviceId);
    Task<DeviceGame> AddAsync(DeviceGame deviceGame);
    Task UpdateAsync(DeviceGame deviceGame);
    Task DeleteAsync(Guid deviceId, Guid gameId);
}
