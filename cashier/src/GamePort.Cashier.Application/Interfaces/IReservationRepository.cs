using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<IEnumerable<Reservation>> GetAllAsync();
    Task<IEnumerable<Reservation>> GetByDateAsync(DateTime date);
    Task<IEnumerable<Reservation>> GetUpcomingAsync(DateTime from);
    Task<IEnumerable<Reservation>> GetOverlappingAsync(Guid deviceId, DateTime start, DateTime end);
    Task<IEnumerable<Reservation>> GetExpiredAsync(DateTime referenceTime);
    Task<Reservation> CreateAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
}
