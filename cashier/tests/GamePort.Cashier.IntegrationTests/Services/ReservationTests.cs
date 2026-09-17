using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Reservations;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class ReservationTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public CashierDbContext Context { get; }
        public DeviceRepository Devices { get; }
        public CustomerRepository Customers { get; }
        public SessionRepository Sessions { get; }
        public ReservationRepository Reservations { get; }
        public PricingRuleRepository Pricing { get; }
        public AuditLogRepository AuditLogs { get; }
        public OutboxRepository Outbox { get; }
        public UnitOfWork UnitOfWork { get; }

        private Fixture(CashierDbContext context)
        {
            Context = context;
            Devices = new DeviceRepository(context);
            Customers = new CustomerRepository(context);
            Sessions = new SessionRepository(context);
            Reservations = new ReservationRepository(context);
            Pricing = new PricingRuleRepository(context);
            AuditLogs = new AuditLogRepository(context);
            Outbox = new OutboxRepository(context);
            UnitOfWork = new UnitOfWork(context);
        }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CashierDbContext>()
                .UseInMemoryDatabase($"wingport-reservation-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public async Task<Customer> CreateCustomerAsync(string phone = "09120000000")
        {
            var handler = new RegisterCustomerHandler(Customers, new WalletRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new RegisterCustomerCommand("Ali", phone));
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

        public CreateReservationHandler CreateHandler() =>
            new(Reservations, Customers, Devices, Sessions, AuditLogs, Outbox, UnitOfWork);

        public ArriveReservationHandler ArriveHandler() =>
            new(Reservations,
                new StartSessionHandler(Customers, Devices, Sessions, Pricing, Reservations, AuditLogs, Outbox, UnitOfWork),
                AuditLogs, Outbox, UnitOfWork);

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task CreateReservation_Succeeds_AndRejectsOverlap()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        var handler = fixture.CreateHandler();

        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var first = await handler.HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, start, end, null, "Cashier", "Cashier"));
        Assert.True(first.IsSuccess);

        var overlapping = await handler.HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, start.AddMinutes(30), end.AddMinutes(30), null, "Cashier", "Cashier"));

        Assert.False(overlapping.IsSuccess);
        Assert.Equal(1, await fixture.Context.Reservations.CountAsync());
    }

    [Fact]
    public async Task CancelReservation_MarksCancelled()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        var create = await fixture.CreateHandler().HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), null, "Cashier", "Cashier"));

        var cancelHandler = new CancelReservationHandler(fixture.Reservations, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var result = await cancelHandler.HandleAsync(new CancelReservationCommand(create.Value!.ReservationId, "no longer needed", "Cashier"));

        Assert.True(result.IsSuccess);
        var stored = await fixture.Reservations.GetByIdAsync(create.Value.ReservationId);
        Assert.Equal(ReservationStatus.Cancelled, stored!.Status);
    }

    [Fact]
    public async Task ArriveEarly_KeepsReservedDuration()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();

        var start = DateTime.UtcNow.AddMinutes(30);
        var end = start.AddHours(1);
        var create = await fixture.CreateHandler().HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, start, end, null, "Cashier", "Cashier"));

        var result = await fixture.ArriveHandler().HandleAsync(
            new ArriveReservationCommand(create.Value!.ReservationId, "Cashier"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WasEarly);
        Assert.Equal(1.0, (result.Value.PlannedEndTime!.Value - result.Value.StartTime).TotalHours, 1);

        var session = await fixture.Sessions.GetByIdAsync(result.Value.SessionId);
        Assert.Equal(SessionStatus.Active, session!.Status);

        var reservation = await fixture.Reservations.GetByIdAsync(create.Value.ReservationId);
        Assert.Equal(ReservationStatus.Converted, reservation!.Status);
        Assert.Equal(result.Value.SessionId, reservation.ConvertedSessionId);
    }

    [Fact]
    public async Task ArriveLate_KeepsReservedEndTime()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();

        var start = DateTime.UtcNow.AddMinutes(-10);
        var end = DateTime.UtcNow.AddMinutes(50);
        var create = await fixture.CreateHandler().HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, start, end, null, "Cashier", "Cashier"));

        var result = await fixture.ArriveHandler().HandleAsync(
            new ArriveReservationCommand(create.Value!.ReservationId, "Cashier"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.WasEarly);
        Assert.Equal(end, result.Value.PlannedEndTime);
    }

    [Fact]
    public async Task ExpireReservations_MarksPastDueAsExpired()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();

        var reservation = await fixture.Reservations.CreateAsync(new Reservation
        {
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = device.Type,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = ReservationStatus.Reserved
        });

        var handler = new ExpireReservationsHandler(fixture.Reservations, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var result = await handler.HandleAsync();

        Assert.Equal(1, result.ExpiredCount);
        var stored = await fixture.Reservations.GetByIdAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Expired, stored!.Status);
    }
}
