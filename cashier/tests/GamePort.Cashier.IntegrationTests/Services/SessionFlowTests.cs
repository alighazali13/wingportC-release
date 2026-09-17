using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class SessionFlowTests
{
    private static CashierDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseInMemoryDatabase($"wingport-test-{Guid.NewGuid():N}")
            .Options;

        return new CashierDbContext(options);
    }

    [Fact]
    public async Task RegisterDevice_PersistsDeviceWithHashedSecret_AndWritesAuditAndOutbox()
    {
        using var context = CreateContext();
        var devices = new DeviceRepository(context);
        var auditLogs = new AuditLogRepository(context);
        var outbox = new OutboxRepository(context);
        var hasher = new Pbkdf2SecretHasher();
        var unitOfWork = new UnitOfWork(context);
        var handler = new RegisterDeviceHandler(devices, auditLogs, outbox, hasher, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterDeviceCommand("PC-01", DeviceType.GamingPC, "10.0.0.5", null, null));

        Assert.True(result.IsSuccess);
        var value = result.Value!;
        Assert.NotEqual(Guid.Empty, value.Device.Id);
        Assert.False(string.IsNullOrEmpty(value.ClientSecret));
        Assert.NotEqual(value.ClientSecret, value.Device.ClientSecretHash);
        Assert.True(hasher.Verify(value.ClientSecret, value.Device.ClientSecretHash!));
        Assert.Equal(1, await context.AuditLogs.CountAsync());
        Assert.Equal(1, await context.OutboxEvents.CountAsync());
    }

    [Fact]
    public async Task StartSession_FreezesPriceAndMarksDeviceInUse_ThenEndReleasesDevice()
    {
        using var context = CreateContext();
        var devices = new DeviceRepository(context);
        var customers = new CustomerRepository(context);
        var sessions = new SessionRepository(context);
        var pricing = new PricingRuleRepository(context);
        var auditLogs = new AuditLogRepository(context);
        var outbox = new OutboxRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var billing = new Application.Services.SessionBillingService(
            sessions,
            new Application.Services.WalletService(new WalletRepository(context), auditLogs, outbox, unitOfWork),
            unitOfWork);

        var customer = await customers.CreateAsync(new Customer { Name = "Ali", PhoneNumber = "09120000000" });
        var device = await devices.CreateAsync(new Device
        {
            Name = "PC-01",
            Type = DeviceType.GamingPC,
            Status = DeviceStatus.Available
        });
        await pricing.AddAsync(new PricingRule
        {
            Name = "PC hourly",
            DeviceType = DeviceType.GamingPC,
            PricePerHour = 100_000m,
            IsActive = true
        });

        var startHandler = new StartSessionHandler(customers, devices, sessions, pricing, new ReservationRepository(context), auditLogs, outbox, unitOfWork);
        var startResult = await startHandler.HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));

        Assert.True(startResult.IsSuccess);
        Assert.Equal(100_000m, startResult.Value!.PriceAtStart);

        var deviceInUse = await context.Devices.FindAsync(device.Id);
        Assert.Equal(DeviceStatus.InUse, deviceInUse!.Status);

        var session = await context.Sessions.FirstAsync();
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(100_000m, session.PriceAtStart);

        var endHandler = new EndSessionHandler(sessions, devices, billing, auditLogs, outbox, unitOfWork);
        var endResult = await endHandler.HandleAsync(
            new EndSessionCommand(startResult.Value.SessionId, "customer left", "Cashier"));

        Assert.True(endResult.IsSuccess);

        var releasedDevice = await context.Devices.FindAsync(device.Id);
        Assert.Equal(DeviceStatus.Available, releasedDevice!.Status);

        var endedSession = await context.Sessions.FirstAsync();
        Assert.Equal(SessionStatus.Completed, endedSession.Status);
        Assert.NotNull(endedSession.ActualEndTime);
    }

    [Fact]
    public async Task StartSession_Fails_WhenDeviceAlreadyInUse()
    {
        using var context = CreateContext();
        var devices = new DeviceRepository(context);
        var customers = new CustomerRepository(context);
        var sessions = new SessionRepository(context);
        var pricing = new PricingRuleRepository(context);
        var auditLogs = new AuditLogRepository(context);
        var outbox = new OutboxRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var customer = await customers.CreateAsync(new Customer { Name = "Sara", PhoneNumber = "09121111111" });
        var device = await devices.CreateAsync(new Device
        {
            Name = "PC-02",
            Type = DeviceType.GamingPC,
            Status = DeviceStatus.InUse
        });
        await pricing.AddAsync(new PricingRule
        {
            Name = "PC hourly",
            DeviceType = DeviceType.GamingPC,
            PricePerHour = 100_000m,
            IsActive = true
        });

        var handler = new StartSessionHandler(customers, devices, sessions, pricing, new ReservationRepository(context), auditLogs, outbox, unitOfWork);
        var result = await handler.HandleAsync(new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Sessions.CountAsync());
    }

    [Fact]
    public async Task StartSession_Fails_WhenNoActivePricingRule()
    {
        using var context = CreateContext();
        var devices = new DeviceRepository(context);
        var customers = new CustomerRepository(context);
        var sessions = new SessionRepository(context);
        var pricing = new PricingRuleRepository(context);
        var auditLogs = new AuditLogRepository(context);
        var outbox = new OutboxRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var customer = await customers.CreateAsync(new Customer { Name = "Nima", PhoneNumber = "09122222222" });
        var device = await devices.CreateAsync(new Device
        {
            Name = "PS-01",
            Type = DeviceType.PlayStation,
            Status = DeviceStatus.Available
        });

        var handler = new StartSessionHandler(customers, devices, sessions, pricing, new ReservationRepository(context), auditLogs, outbox, unitOfWork);
        var result = await handler.HandleAsync(new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));

        Assert.False(result.IsSuccess);
    }
}
