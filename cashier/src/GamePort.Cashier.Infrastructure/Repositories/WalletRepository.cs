using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly CashierDbContext _context;

    public WalletRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Wallet?> GetByIdAsync(Guid id)
    {
        return await _context.Wallets
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<Wallet?> GetByCustomerIdAsync(Guid customerId)
    {
        return await _context.Wallets
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.CustomerId == customerId);
    }

    public async Task<IEnumerable<Wallet>> GetAllAsync()
    {
        return await _context.Wallets.ToListAsync();
    }

    public async Task<Wallet> CreateAsync(Wallet wallet)
    {
        wallet.Id = Guid.NewGuid();
        wallet.CreatedAt = DateTime.UtcNow;
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();
        return wallet;
    }

    public async Task UpdateAsync(Wallet wallet)
    {
        wallet.UpdatedAt = DateTime.UtcNow;
        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();
    }

    public async Task AddTransactionAsync(WalletTransaction transaction)
    {
        if (transaction.Id == Guid.Empty)
        {
            transaction.Id = Guid.NewGuid();
        }

        transaction.CreatedAt = DateTime.UtcNow;
        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task<WalletTransaction?> GetTransactionByIdempotencyKeyAsync(string idempotencyKey)
    {
        return await _context.WalletTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
    }

    public async Task<IEnumerable<WalletTransaction>> GetTransactionsAsync(Guid walletId, int limit)
    {
        return await _context.WalletTransactions
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<WalletTransaction>> GetTransactionsByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.WalletTransactions
            .Where(t => t.CreatedAt >= from && t.CreatedAt < to)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}
