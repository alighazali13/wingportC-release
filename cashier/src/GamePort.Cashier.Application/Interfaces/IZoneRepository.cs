using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IZoneRepository
{
    Task<Zone?> GetByIdAsync(Guid id);
    Task<IEnumerable<Zone>> GetAllAsync();
    Task<Zone> CreateAsync(Zone zone);
    Task UpdateAsync(Zone zone);
    Task DeleteAsync(Guid id);
    Task<bool> HasChildrenAsync(Guid zoneId);
    Task<bool> HasDevicesAsync(Guid zoneId);
}
