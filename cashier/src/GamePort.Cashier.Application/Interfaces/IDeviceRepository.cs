using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id);
    Task<Device?> GetByClientIdentityAsync(string clientIdentity);
    Task<IEnumerable<Device>> GetAllAsync();
    Task<IEnumerable<Device>> GetByTypeAsync(Domain.Enums.DeviceType type);
    Task<Device> CreateAsync(Device device);
    Task UpdateAsync(Device device);
    Task DeleteAsync(Guid id);
}
