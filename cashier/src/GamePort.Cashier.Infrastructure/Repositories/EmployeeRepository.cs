using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly CashierDbContext _context;

    public EmployeeRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Employee?> GetByIdAsync(Guid id)
    {
        return await _context.Employees.FindAsync(id);
    }

    public async Task<Employee?> GetByUsernameAsync(string username)
    {
        return await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
    }

    public async Task<IEnumerable<Employee>> GetAllAsync()
    {
        return await _context.Employees.OrderBy(e => e.Name).ToListAsync();
    }

    public async Task<int> CountAsync()
    {
        return await _context.Employees.CountAsync();
    }

    public async Task<Employee> CreateAsync(Employee employee)
    {
        employee.Id = Guid.NewGuid();
        employee.CreatedAt = DateTime.UtcNow;
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        return employee;
    }

    public async Task UpdateAsync(Employee employee)
    {
        employee.UpdatedAt = DateTime.UtcNow;
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();
    }
}
