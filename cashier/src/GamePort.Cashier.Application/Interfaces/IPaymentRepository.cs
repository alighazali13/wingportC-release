using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id);
    Task<IEnumerable<Payment>> GetByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<Payment>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<Payment> AddAsync(Payment payment);
}
