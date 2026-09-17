using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id);
    Task<Employee?> GetByUsernameAsync(string username);
    Task<IEnumerable<Employee>> GetAllAsync();
    Task<int> CountAsync();
    Task<Employee> CreateAsync(Employee employee);
    Task UpdateAsync(Employee employee);
}
