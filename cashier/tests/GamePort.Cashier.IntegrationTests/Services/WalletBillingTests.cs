using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Application.UseCases.Wallets;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class WalletBillingTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public CashierDbContext Context { get; }
        public DeviceRepository Devices { get; }
        public CustomerRepository Customers { get; }
        public SessionRepository Sessions { get; }
        public PricingRuleRepository Pricing { get; }
        public AuditLogRepository AuditLogs { get; }
        public OutboxRepository Outbox { get; }
        public UnitOfWork UnitOfWork { get; }
        public WalletRepository Wallets { get; }
        public Application.Services.WalletService WalletService { get; }
        public Application.Services.SessionBillingService Billing { get; }

        private Fixture(CashierDbContext context)
        {
            Context = context;
            Devices = new DeviceRepository(context);
            Customers = new CustomerRepository(context);
            Sessions = new SessionRepository(context);
            Pricing = new PricingRuleRepository(context);
            AuditLogs = new AuditLogRepository(context);
            Outbox = new OutboxRepository(context);
            UnitOfWork = new UnitOfWork(context);
            Wallets = new WalletRepository(context);
            WalletService = new Application.Services.WalletService(Wallets, AuditLogs, Outbox, UnitOfWork);
            Billing = new Application.Services.SessionBillingService(Sessions, WalletService, UnitOfWork);
        }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CashierDbContext>()
                .UseInMemoryDatabase($"wingport-wallet-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public async Task<Customer> CreateCustomerAsync()
        {
            var handler = new RegisterCustomerHandler(Customers, Wallets, AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new RegisterCustomerCommand("Ali", null));
            return result.Value!.Customer;
        }

        public async Task<Device> CreateDeviceAsync(string name = "PC-01")
        {
            await Pricing.AddAsync(new PricingRule
            {
                Name = "PC hourly",
                DeviceType = DeviceType.GamingPC,
                PricePerHour = 100_000m,
                IsActive = true
            });

            return await Devices.CreateAsync(new Device { Name = name, Type = DeviceType.GamingPC, Status = DeviceStatus.Available });
        }

        public async Task<Session> StartSessionAsync(Guid customerId, Guid deviceId)
        {
            var handler = new StartSessionHandler(Customers, Devices, Sessions, Pricing, new ReservationRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new StartSessionCommand(customerId, deviceId, null, "Cashier"));
            Assert.True(result.IsSuccess);
            return (await Sessions.GetByIdAsync(result.Value!.SessionId))!;
        }

        public Task RechargeAsync(Guid customerId, decimal amount)
        {
            var handler = new RechargeWalletHandler(WalletService, new PaymentRepository(Context));
            return handler.HandleAsync(new RechargeWalletCommand(customerId, amount, PaymentMethod.Cash, "Cashier", null));
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task EndSession_ChargesElapsedTimeFromWallet()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        await fixture.RechargeAsync(customer.Id, 100_000m);

        var session = await fixture.StartSessionAsync(customer.Id, device.Id);

        session.StartTime = DateTime.UtcNow.AddMinutes(-30);
        session.LastChargedAt = null;
        await fixture.Sessions.UpdateAsync(session);

        var handler = new EndSessionHandler(
            fixture.Sessions, fixture.Devices, fixture.Billing, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);

        var result = await handler.HandleAsync(new EndSessionCommand(session.Id, null, "Cashier"));

        Assert.True(result.IsSuccess);
        var balance = await fixture.WalletService.GetBalanceAsync(customer.Id);
        Assert.InRange(balance, 49_990m, 50_000m);

        var stored = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(100_000m - balance, stored!.TotalCharged);
    }

    [Fact]
    public async Task BillingWorker_InterruptsSession_WhenFundsRunOut()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        await fixture.RechargeAsync(customer.Id, 1_000m);

        var session = await fixture.StartSessionAsync(customer.Id, device.Id);

        session.StartTime = DateTime.UtcNow.AddHours(-1);
        session.LastChargedAt = null;
        await fixture.Sessions.UpdateAsync(session);

        var handler = new ChargeActiveSessionsHandler(
            fixture.Sessions, fixture.Devices, fixture.Billing, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);

        var result = await handler.HandleAsync();

        Assert.Equal(1, result.InterruptedCount);

        var stored = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(SessionStatus.Interrupted, stored!.Status);
        Assert.Equal(0m, await fixture.WalletService.GetBalanceAsync(customer.Id));
        Assert.Equal(DeviceStatus.Available, (await fixture.Devices.GetByIdAsync(device.Id))!.Status);
    }

    [Fact]
    public async Task Recharge_IsIdempotent_ForSameKey()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();

        var first = await fixture.WalletService.RechargeAsync(customer.Id, 50_000m, "test", "recharge-fixed-key");
        var second = await fixture.WalletService.RechargeAsync(customer.Id, 50_000m, "test", "recharge-fixed-key");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.Value!.AlreadyApplied);
        Assert.Equal(50_000m, await fixture.WalletService.GetBalanceAsync(customer.Id));
    }

    [Fact]
    public async Task Recharge_CreatesImmutableTransactionWithBalanceAfter()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();

        await fixture.RechargeAsync(customer.Id, 20_000m);
        await fixture.RechargeAsync(customer.Id, 30_000m);

        var wallet = await fixture.Wallets.GetByCustomerIdAsync(customer.Id);
        var transactions = (await fixture.Wallets.GetTransactionsAsync(wallet!.Id, 10)).OrderBy(t => t.CreatedAt).ToList();

        Assert.Equal(2, transactions.Count);
        Assert.Equal(20_000m, transactions[0].BalanceAfter);
        Assert.Equal(50_000m, transactions[1].BalanceAfter);
        Assert.All(transactions, t => Assert.Equal(WalletTransactionTypes.Recharge, t.Type));
    }
}
