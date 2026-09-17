using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly CashierDbContext _context;

    public UnitOfWork(CashierDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            await action(cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
