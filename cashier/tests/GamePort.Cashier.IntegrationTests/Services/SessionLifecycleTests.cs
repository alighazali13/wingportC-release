using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Contracts.Hub;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class SessionLifecycleTests
{
    private sealed class RecordingCommandSender : IDeviceCommandSender
    {
        public List<(Guid DeviceId, DeviceCommandMessage Command)> Sent { get; } = new();

        public Task<bool> SendAsync(Guid deviceId, DeviceCommandMessage command, CancellationToken cancellationToken = default)
        {
            Sent.Add((deviceId, command));
            return Task.FromResult(true);
        }
    }

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
        public RecordingCommandSender CommandSender { get; } = new();

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
                .UseInMemoryDatabase($"wingport-session-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public async Task EnsurePricingAsync(DeviceType type, decimal price = 100_000m)
        {
            await Pricing.AddAsync(new PricingRule
            {
                Name = $"{type} hourly",
                DeviceType = type,
                PricePerHour = price,
                IsActive = true
            });
        }

        public async Task<Customer> CreateCustomerAsync(string name = "Ali", string phone = "09120000000")
        {
            var handler = new RegisterCustomerHandler(Customers, new WalletRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new RegisterCustomerCommand(name, phone));
            return result.Value!.Customer;
        }

        public async Task<Device> CreateDeviceAsync(string name, DeviceType type, DeviceStatus status = DeviceStatus.Available)
        {
            return await Devices.CreateAsync(new Device { Name = name, Type = type, Status = status });
        }

        public async Task<Session> StartSessionAsync(Guid customerId, Guid deviceId, DateTime? plannedEnd = null)
        {
            var handler = new StartSessionHandler(Customers, Devices, Sessions, Pricing, new ReservationRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new StartSessionCommand(customerId, deviceId, plannedEnd, "Cashier"));
            Assert.True(result.IsSuccess);
            return (await Sessions.GetByIdAsync(result.Value!.SessionId))!;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task Extend_AddsMinutesToPlannedEnd()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync("PC-01", DeviceType.GamingPC);
        var session = await fixture.StartSessionAsync(customer.Id, device.Id, DateTime.UtcNow.AddHours(1));
        var originalPlannedEnd = session.PlannedEndTime!.Value;

        var handler = new ExtendSessionHandler(fixture.Sessions, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var result = await handler.HandleAsync(new ExtendSessionCommand(session.Id, null, 30, "Cashier"));

        Assert.True(result.IsSuccess);
        var updated = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(originalPlannedEnd.AddMinutes(30), updated!.PlannedEndTime);
    }

    [Fact]
    public async Task PauseAndResume_AccumulatesPausedTime()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync("PC-02", DeviceType.GamingPC);
        var session = await fixture.StartSessionAsync(customer.Id, device.Id);

        var pauseHandler = new PauseSessionHandler(fixture.Sessions, fixture.Billing, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var pauseResult = await pauseHandler.HandleAsync(new PauseSessionCommand(session.Id, "Cashier"));
        Assert.True(pauseResult.IsSuccess);

        var paused = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(SessionStatus.Paused, paused!.Status);

        paused.PausedAt = DateTime.UtcNow.AddSeconds(-30);
        await fixture.Sessions.UpdateAsync(paused);

        var resumeHandler = new ResumeSessionHandler(fixture.Sessions, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var resumeResult = await resumeHandler.HandleAsync(new ResumeSessionCommand(session.Id, "Cashier"));

        Assert.True(resumeResult.IsSuccess);
        Assert.True(resumeResult.Value!.TotalPausedSeconds >= 30);

        var resumed = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(SessionStatus.Active, resumed!.Status);
        Assert.Null(resumed.PausedAt);
    }

    [Fact]
    public async Task Transfer_MovesSessionToNewDevice_AndNotifiesBothClients()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var source = await fixture.CreateDeviceAsync("PC-01", DeviceType.GamingPC);
        var target = await fixture.CreateDeviceAsync("PC-02", DeviceType.GamingPC);
        var session = await fixture.StartSessionAsync(customer.Id, source.Id);

        var handler = new TransferSessionHandler(
            fixture.Sessions, fixture.Devices, fixture.AuditLogs, fixture.Outbox, fixture.CommandSender, fixture.UnitOfWork);

        var result = await handler.HandleAsync(new TransferSessionCommand(session.Id, target.Id, "Cashier"));

        Assert.True(result.IsSuccess);
        Assert.Equal(source.Id, result.Value!.FromDeviceId);
        Assert.Equal(target.Id, result.Value.ToDeviceId);

        var movedSession = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(target.Id, movedSession!.DeviceId);

        Assert.Equal(DeviceStatus.Available, (await fixture.Devices.GetByIdAsync(source.Id))!.Status);
        Assert.Equal(DeviceStatus.InUse, (await fixture.Devices.GetByIdAsync(target.Id))!.Status);

        Assert.Contains(fixture.CommandSender.Sent, s => s.DeviceId == target.Id && s.Command.CommandType == DeviceCommandType.RestoreSession);
        Assert.Contains(fixture.CommandSender.Sent, s => s.DeviceId == source.Id && s.Command.CommandType == DeviceCommandType.RefreshState);
    }

    [Fact]
    public async Task Transfer_Fails_WhenTargetDeviceIsInUse()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var source = await fixture.CreateDeviceAsync("PC-01", DeviceType.GamingPC);
        var target = await fixture.CreateDeviceAsync("PC-02", DeviceType.GamingPC, DeviceStatus.InUse);
        var session = await fixture.StartSessionAsync(customer.Id, source.Id);

        var handler = new TransferSessionHandler(
            fixture.Sessions, fixture.Devices, fixture.AuditLogs, fixture.Outbox, fixture.CommandSender, fixture.UnitOfWork);

        var result = await handler.HandleAsync(new TransferSessionCommand(session.Id, target.Id, "Cashier"));

        Assert.False(result.IsSuccess);
        Assert.Empty(fixture.CommandSender.Sent);
    }

    [Fact]
    public async Task Transfer_Fails_WhenDeviceTypeDiffers()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var source = await fixture.CreateDeviceAsync("PC-01", DeviceType.GamingPC);
        var console = await fixture.CreateDeviceAsync("PS-01", DeviceType.PlayStation);
        var session = await fixture.StartSessionAsync(customer.Id, source.Id);

        var handler = new TransferSessionHandler(
            fixture.Sessions, fixture.Devices, fixture.AuditLogs, fixture.Outbox, fixture.CommandSender, fixture.UnitOfWork);

        var result = await handler.HandleAsync(new TransferSessionCommand(session.Id, console.Id, "Cashier"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Cancel_ReleasesDeviceAndClosesSession()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync("PC-03", DeviceType.GamingPC);
        var session = await fixture.StartSessionAsync(customer.Id, device.Id);

        var handler = new CancelSessionHandler(fixture.Sessions, fixture.Devices, fixture.Billing, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var result = await handler.HandleAsync(new CancelSessionCommand(session.Id, "customer changed mind", "Cashier"));

        Assert.True(result.IsSuccess);
        var cancelled = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(SessionStatus.Cancelled, cancelled!.Status);
        Assert.Equal(DeviceStatus.Available, (await fixture.Devices.GetByIdAsync(device.Id))!.Status);
    }

    [Fact]
    public async Task Expire_ClosesPastDueSessionsAndFreesDevice()
    {
        await using var fixture = Fixture.Create();
        await fixture.EnsurePricingAsync(DeviceType.GamingPC);
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync("PC-04", DeviceType.GamingPC);
        var session = await fixture.StartSessionAsync(customer.Id, device.Id, DateTime.UtcNow.AddMinutes(-5));

        var handler = new ExpireSessionsHandler(
            fixture.Sessions, fixture.Devices, fixture.Billing, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);

        var result = await handler.HandleAsync();

        Assert.Equal(1, result.ExpiredCount);
        var expired = await fixture.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(SessionStatus.Expired, expired!.Status);
        Assert.Equal(DeviceStatus.Available, (await fixture.Devices.GetByIdAsync(device.Id))!.Status);
    }
}
