using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Customer?> GetByPhoneNumberAsync(string phoneNumber);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<IEnumerable<Customer>> SearchAsync(string query);
    Task<Customer> CreateAsync(Customer customer);
    Task UpdateAsync(Customer customer);
}
