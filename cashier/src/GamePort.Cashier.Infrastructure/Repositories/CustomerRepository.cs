using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly CashierDbContext _context;

    public CustomerRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        return await _context.Customers.FindAsync(id);
    }

    public async Task<Customer?> GetByPhoneNumberAsync(string phoneNumber)
    {
        return await _context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await _context.Customers.ToListAsync();
    }

    public async Task<IEnumerable<Customer>> SearchAsync(string query)
    {
        return await _context.Customers
            .Where(c => c.Name.Contains(query) || c.PhoneNumber!.Contains(query))
            .ToListAsync();
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        customer.Id = Guid.NewGuid();
        customer.CreatedAt = DateTime.UtcNow;
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task UpdateAsync(Customer customer)
    {
        customer.UpdatedAt = DateTime.UtcNow;
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();
    }
}
