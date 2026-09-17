using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id);
    Task<IEnumerable<Session>> GetAllAsync();
    Task<IEnumerable<Session>> GetActiveSessionsAsync();
    Task<IEnumerable<Session>> GetOpenSessionsAsync();
    Task<IEnumerable<Session>> GetByCustomerIdAsync(Guid customerId);
    Task<Session?> GetOpenByDeviceIdAsync(Guid deviceId);
    Task<IEnumerable<Session>> GetExpiredAsync(DateTime referenceTime);
    Task<IEnumerable<Session>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<Session> CreateAsync(Session session);
    Task UpdateAsync(Session session);
}
