using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly CashierDbContext _context;

    public PaymentRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(Guid id)
    {
        return await _context.Payments.FindAsync(id);
    }

    public async Task<IEnumerable<Payment>> GetByCustomerIdAsync(Guid customerId)
    {
        return await _context.Payments
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Payment>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.Payments
            .Where(p => p.CreatedAt >= from && p.CreatedAt < to)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Payment> AddAsync(Payment payment)
    {
        payment.Id = Guid.NewGuid();
        payment.CreatedAt = DateTime.UtcNow;
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }
}
