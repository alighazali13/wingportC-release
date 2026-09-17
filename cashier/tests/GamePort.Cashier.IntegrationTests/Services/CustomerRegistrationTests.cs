using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class CustomerRegistrationTests
{
    private static CashierDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseInMemoryDatabase($"wingport-test-{Guid.NewGuid():N}")
            .Options;

        return new CashierDbContext(options);
    }

    [Fact]
    public async Task RegisterCustomer_CreatesCustomerWithEmptyWallet_AndRejectsDuplicatePhone()
    {
        using var context = CreateContext();
        var handler = new RegisterCustomerHandler(
            new CustomerRepository(context),
            new WalletRepository(context),
            new AuditLogRepository(context),
            new OutboxRepository(context),
            new UnitOfWork(context));

        var result = await handler.HandleAsync(new RegisterCustomerCommand("Ali", "09120000000"));

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value!.Wallet.Balance);
        Assert.Equal(1, await context.Customers.CountAsync());
        Assert.Equal(1, await context.Wallets.CountAsync());

        var duplicate = await handler.HandleAsync(new RegisterCustomerCommand("Ali 2", "09120000000"));

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(1, await context.Customers.CountAsync());
    }
}
