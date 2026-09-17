using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Reports;
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

public class FinancialReportTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public CashierDbContext Context { get; }
        public DeviceRepository Devices { get; }
        public CustomerRepository Customers { get; }
        public SessionRepository Sessions { get; }
        public PricingRuleRepository Pricing { get; }
        public WalletRepository Wallets { get; }
        public PaymentRepository Payments { get; }
        public AuditLogRepository AuditLogs { get; }
        public OutboxRepository Outbox { get; }
        public UnitOfWork UnitOfWork { get; }
        public Application.Services.WalletService WalletService { get; }
        public Application.Services.SessionBillingService Billing { get; }

        private Fixture(CashierDbContext context)
        {
            Context = context;
            Devices = new DeviceRepository(context);
            Customers = new CustomerRepository(context);
            Sessions = new SessionRepository(context);
            Pricing = new PricingRuleRepository(context);
            Wallets = new WalletRepository(context);
            Payments = new PaymentRepository(context);
            AuditLogs = new AuditLogRepository(context);
            Outbox = new OutboxRepository(context);
            UnitOfWork = new UnitOfWork(context);
            WalletService = new Application.Services.WalletService(Wallets, AuditLogs, Outbox, UnitOfWork);
            Billing = new Application.Services.SessionBillingService(Sessions, WalletService, UnitOfWork);
        }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CashierDbContext>()
                .UseInMemoryDatabase($"wingport-report-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public async Task<Customer> CreateCustomerAsync()
        {
            var handler = new RegisterCustomerHandler(Customers, Wallets, AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new RegisterCustomerCommand("Ali", "09120000000"));
            return result.Value!.Customer;
        }

        public async Task<Device> CreateDeviceAsync()
        {
            await Pricing.AddAsync(new PricingRule
            {
                Name = "PC hourly",
                DeviceType = DeviceType.GamingPC,
                PricePerHour = 100_000m,
                IsActive = true
            });

            return await Devices.CreateAsync(new Device { Name = "PC-01", Type = DeviceType.GamingPC, Status = DeviceStatus.Available });
        }

        public async Task RechargeAsync(Guid customerId, decimal amount)
        {
            var handler = new RechargeWalletHandler(WalletService, Payments);
            var result = await handler.HandleAsync(new RechargeWalletCommand(customerId, amount, PaymentMethod.Cash, "Cashier", null));
            Assert.True(result.IsSuccess);
        }

        public async Task<Session> StartSessionAsync(Guid customerId, Guid deviceId)
        {
            var handler = new StartSessionHandler(Customers, Devices, Sessions, Pricing, new ReservationRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new StartSessionCommand(customerId, deviceId, null, "Cashier"));
            Assert.True(result.IsSuccess);
            return (await Sessions.GetByIdAsync(result.Value!.SessionId))!;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task Report_AggregatesServiceRevenueAndCash()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        await fixture.RechargeAsync(customer.Id, 100_000m);

        var session = await fixture.StartSessionAsync(customer.Id, device.Id);
        session.StartTime = DateTime.UtcNow.AddMinutes(-30);
        session.LastChargedAt = null;
        await fixture.Sessions.UpdateAsync(session);

        var endHandler = new EndSessionHandler(
            fixture.Sessions, fixture.Devices, fixture.Billing, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        await endHandler.HandleAsync(new EndSessionCommand(session.Id, null, "Cashier"));

        var handler = new GetFinancialReportHandler(fixture.Wallets, fixture.Payments, fixture.Customers, fixture.Sessions);
        var report = await handler.HandleAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        Assert.InRange(report.ServiceRevenue, 49_000m, 51_000m);
        Assert.InRange(report.PcRevenue, 49_000m, 51_000m);
        Assert.Equal(0m, report.PsRevenue);
        Assert.Equal(100_000m, report.WalletTopUps);
        Assert.Equal(100_000m, report.CashCollected);
        Assert.Equal(1, report.SessionCount);
        Assert.NotEmpty(report.Transactions);
        Assert.Contains(report.Transactions, t => t.Type == WalletTransactionTypes.SessionCharge && t.CustomerName == "Ali");
    }

    [Fact]
    public async Task Report_ExcludesTransactionsOutsideRange()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        await fixture.RechargeAsync(customer.Id, 50_000m);

        var handler = new GetFinancialReportHandler(fixture.Wallets, fixture.Payments, fixture.Customers, fixture.Sessions);
        var report = await handler.HandleAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-5));

        Assert.Equal(0m, report.WalletTopUps);
        Assert.Equal(0m, report.CashCollected);
    }
}
