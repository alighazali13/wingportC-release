using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByCustomerIdAsync(Guid customerId);
    Task<Wallet?> GetByIdAsync(Guid id);
    Task<IEnumerable<Wallet>> GetAllAsync();
    Task<Wallet> CreateAsync(Wallet wallet);
    Task UpdateAsync(Wallet wallet);
    Task AddTransactionAsync(WalletTransaction transaction);
    Task<WalletTransaction?> GetTransactionByIdempotencyKeyAsync(string idempotencyKey);
    Task<IEnumerable<WalletTransaction>> GetTransactionsAsync(Guid walletId, int limit);
    Task<IEnumerable<WalletTransaction>> GetTransactionsByDateRangeAsync(DateTime from, DateTime to);
}
