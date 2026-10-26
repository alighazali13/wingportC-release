using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Reservations;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// C2-C lifecycle tests: activation, maintenance, deletion, zone-hierarchy guards,
/// session-lifecycle synchronization and SignalR/heartbeat isolation.
/// InMemory provider: xmin is always 0 there; real xmin concurrency is covered by
/// PostgresLifecycleConcurrencyTests.
/// </summary>
public class DeviceLifecycleHandlerTests
{
    private static CashierDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseInMemoryDatabase($"wingport-lifecycle-{Guid.NewGuid():N}")
            .Options;
        return new CashierDbContext(options);
    }

    private static Device NewPc(string name = "PC-01", bool isActive = true,
        OperationalStatus operational = OperationalStatus.Available,
        ConnectivityStatus connectivity = ConnectivityStatus.Disconnected) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Type = DeviceType.GamingPC,
        Status = DeviceStatus.Available,
        IpAddress = "10.0.0.5",
        ConnectionMode = ConnectionMode.Client,
        ConnectivityStatus = connectivity,
        OperationalStatus = operational,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow
    };

    private static Device NewPs5(string name = "PS5-01") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Type = DeviceType.PlayStation,
        Status = DeviceStatus.Available,
        ConnectionMode = ConnectionMode.Manual,
        ConnectivityStatus = ConnectivityStatus.NotApplicable,
        OperationalStatus = OperationalStatus.Available,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static async Task<Customer> SeedCustomerAsync(CashierDbContext context, string phone = "09120000001")
    {
        var customer = new Customer { Name = "Ali", PhoneNumber = phone, CreatedAt = DateTime.UtcNow };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private static async Task<Session> SeedOpenSessionAsync(CashierDbContext context, Device device)
    {
        var customer = await SeedCustomerAsync(context);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = device.Type,
            StartTime = DateTime.UtcNow,
            PriceAtStart = 1000m,
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Sessions.Add(session);
        device.OperationalStatus = OperationalStatus.InUse;
        device.Status = DeviceStatus.InUse;
        await context.SaveChangesAsync();
        return session;
    }

    private static async Task<Reservation> SeedFutureReservationAsync(CashierDbContext context, Device device)
    {
        var customer = await SeedCustomerAsync(context, "09120000002");
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = device.Type,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            Status = ReservationStatus.Reserved,
            CreatedAt = DateTime.UtcNow
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();
        return reservation;
    }

    private static SetDeviceActivationHandler CreateActivationHandler(CashierDbContext context) =>
        new(new DeviceRepository(context), new AuditLogRepository(context), new UnitOfWork(context));

    private static SetDeviceOperationalStatusHandler CreateOpsHandler(CashierDbContext context) =>
        new(new DeviceRepository(context), new SessionRepository(context), new AuditLogRepository(context), new UnitOfWork(context));

    private static DeleteDeviceHandler CreateDeleteHandler(CashierDbContext context) =>
        new(new DeviceRepository(context), new SessionRepository(context), new ReservationRepository(context),
            new AuditLogRepository(context), new UnitOfWork(context));

    private static StartSessionHandler CreateStartSessionHandler(CashierDbContext context) =>
        new(new CustomerRepository(context), new DeviceRepository(context), new ZoneRepository(context),
            new SessionRepository(context), new PricingRuleRepository(context), new ReservationRepository(context),
            new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context));

    private static CreateReservationHandler CreateReservationHandler(CashierDbContext context) =>
        new(new ReservationRepository(context), new CustomerRepository(context), new DeviceRepository(context),
            new ZoneRepository(context), new SessionRepository(context), new AuditLogRepository(context),
            new OutboxRepository(context), new UnitOfWork(context));

    // ===================== Activation =====================

    [Fact]
    public async Task Activation_Deactivate_SetsIsActiveFalse_AndWritesAudit()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(device.Id, false, device.xmin, Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.False(result.Device!.IsActive);
        // Operational state untouched.
        Assert.Equal(OperationalStatus.Available, result.Device.OperationalStatus);
        Assert.Equal(ConnectivityStatus.Disconnected, result.Device.ConnectivityStatus);

        var audit = await context.AuditLogs.SingleAsync(a => a.Action == "DeactivateDevice");
        Assert.Equal(device.Id, audit.EntityId);
        Assert.Contains("false", audit.AfterState!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activation_DeactivateWithActiveSession_Succeeds_AndSessionSurvives()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var session = await SeedOpenSessionAsync(context, device);
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var result = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(device.Id, false, token));

        Assert.True(result.IsSuccess);
        Assert.False(result.Device!.IsActive);

        var surviving = await context.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(SessionStatus.Active, surviving.Status);
        // Session lifecycle state untouched by deactivation.
        Assert.Equal(OperationalStatus.InUse, result.Device.OperationalStatus);
    }

    [Fact]
    public async Task Activation_StaleXmin_ReturnsStaleVersion_WithoutSaving()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(device.Id, false, device.xmin + 999));

        Assert.False(result.IsSuccess);
        Assert.Equal(DeviceLifecycleError.StaleVersion, result.ErrorKind);
        Assert.True((await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).IsActive);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Activation_UnknownDevice_ReturnsNotFound()
    {
        using var context = CreateContext();
        var result = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(Guid.NewGuid(), false, 1));
        Assert.Equal(DeviceLifecycleError.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task Activation_Reactivation_DoesNotResetOperationalStatus()
    {
        using var context = CreateContext();
        var device = NewPc(operational: OperationalStatus.Maintenance);
        device.Status = DeviceStatus.Maintenance;
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        // Deactivate then reactivate while Maintenance.
        var deactivate = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(device.Id, false, token));
        Assert.True(deactivate.IsSuccess);

        var reactivate = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(device.Id, true, deactivate.Device!.xmin));
        Assert.True(reactivate.IsSuccess);
        Assert.True(reactivate.Device!.IsActive);
        Assert.Equal(OperationalStatus.Maintenance, reactivate.Device.OperationalStatus);
        Assert.Equal(DeviceStatus.Maintenance, reactivate.Device.Status);
    }

    [Fact]
    public async Task Activation_InactiveDevice_RejectsNewSession_ButAllowsExistingToEnd()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var session = await SeedOpenSessionAsync(context, device);
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var deactivate = await CreateActivationHandler(context).HandleAsync(
            new SetDeviceActivationCommand(device.Id, false, token));
        Assert.True(deactivate.IsSuccess);

        // New session rejected.
        var customer = await SeedCustomerAsync(context, "09120000003");
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var startResult = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.False(startResult.IsSuccess);

        // Existing session ends normally.
        var billing = new Application.Services.SessionBillingService(
            new SessionRepository(context),
            new Application.Services.WalletService(new WalletRepository(context), new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context)),
            new UnitOfWork(context));
        var endResult = await new EndSessionHandler(
            new SessionRepository(context), new DeviceRepository(context), billing,
            new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context))
            .HandleAsync(new EndSessionCommand(session.Id, null, "Cashier"));
        Assert.True(endResult.IsSuccess);
        Assert.Equal(SessionStatus.Completed, (await context.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id)).Status);
        // Still inactive after session end.
        Assert.False((await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).IsActive);
    }

    [Fact]
    public async Task Activation_InactiveDevice_RejectsNewReservation()
    {
        using var context = CreateContext();
        var device = NewPc(isActive: false);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);

        var result = await CreateReservationHandler(context).HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), null, "Cashier", "Cashier"));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Reservations.CountAsync());
    }

    // ===================== Maintenance =====================

    [Fact]
    public async Task Maintenance_AvailableToMaintenance_Succeeds_SetsNotesAndLastMaintenanceAt()
    {
        using var context = CreateContext();
        var device = NewPc(connectivity: ConnectivityStatus.Connected);
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateOpsHandler(context).HandleAsync(new SetDeviceOperationalStatusCommand(
            device.Id, OperationalStatus.Maintenance, "fan replacement", device.xmin, Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationalStatus.Maintenance, result.Device!.OperationalStatus);
        Assert.Equal(DeviceStatus.Maintenance, result.Device.Status);
        Assert.Equal("fan replacement", result.Device.MaintenanceNotes);
        Assert.NotNull(result.Device.LastMaintenanceAt);
        // Connectivity and activation untouched.
        Assert.Equal(ConnectivityStatus.Connected, result.Device.ConnectivityStatus);
        Assert.True(result.Device.IsActive);

        var audit = await context.AuditLogs.SingleAsync(a => a.Action == "SetDeviceOperationalStatus");
        Assert.Contains("Maintenance", audit.AfterState!);
    }

    [Fact]
    public async Task Maintenance_MaintenanceToAvailable_Succeeds_LegacySyncsByConnectivity()
    {
        using var context = CreateContext();
        var device = NewPc(operational: OperationalStatus.Maintenance, connectivity: ConnectivityStatus.Disconnected);
        device.Status = DeviceStatus.Maintenance;
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateOpsHandler(context).HandleAsync(new SetDeviceOperationalStatusCommand(
            device.Id, OperationalStatus.Available, null, device.xmin));

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationalStatus.Available, result.Device!.OperationalStatus);
        // Disconnected GamingPC: legacy Status reflects connectivity.
        Assert.Equal(DeviceStatus.Disconnected, result.Device.Status);
        Assert.NotNull(result.Device.LastMaintenanceAt);
    }

    [Fact]
    public async Task Maintenance_InUseToMaintenance_Rejected()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        await SeedOpenSessionAsync(context, device);
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var result = await CreateOpsHandler(context).HandleAsync(new SetDeviceOperationalStatusCommand(
            device.Id, OperationalStatus.Maintenance, null, token));

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalStatus.InUse, (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).OperationalStatus);
        Assert.Equal(0, await context.AuditLogs.CountAsync(a => a.Action == "SetDeviceOperationalStatus"));
    }

    [Fact]
    public async Task Maintenance_ActiveSessionBlocksMaintenance_WithConflict()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        // Open session exists but device operational state was not yet InUse (race simulation).
        var session = await SeedOpenSessionAsync(context, device);
        // Force operational back to Available while keeping the session (inconsistent legacy row).
        var tracked = await context.Devices.SingleAsync(d => d.Id == device.Id);
        tracked.OperationalStatus = OperationalStatus.Available;
        tracked.Status = DeviceStatus.Available;
        await context.SaveChangesAsync();
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var result = await CreateOpsHandler(context).HandleAsync(new SetDeviceOperationalStatusCommand(
            device.Id, OperationalStatus.Maintenance, null, token));

        Assert.False(result.IsSuccess);
        Assert.Equal(DeviceLifecycleError.Conflict, result.ErrorKind);
    }

    [Fact]
    public void Maintenance_ManualInUse_RejectedByValidator()
    {
        var validator = new Application.Validators.OperationalStatusRequestValidator();
        var result = validator.Validate(new Contracts.Requests.OperationalStatusRequest
        {
            OperationalStatus = "InUse",
            Xmin = "1"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Maintenance_AlreadyInMaintenance_Rejected()
    {
        using var context = CreateContext();
        var device = NewPc(operational: OperationalStatus.Maintenance);
        device.Status = DeviceStatus.Maintenance;
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateOpsHandler(context).HandleAsync(new SetDeviceOperationalStatusCommand(
            device.Id, OperationalStatus.Maintenance, null, device.xmin));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Maintenance_StaleXmin_ReturnsStale()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateOpsHandler(context).HandleAsync(new SetDeviceOperationalStatusCommand(
            device.Id, OperationalStatus.Maintenance, null, device.xmin + 500));

        Assert.False(result.IsSuccess);
        Assert.Equal(DeviceLifecycleError.StaleVersion, result.ErrorKind);
    }

    [Fact]
    public async Task Maintenance_Reconnect_DoesNotClearMaintenance()
    {
        using var context = CreateContext();
        var device = NewPc(operational: OperationalStatus.Maintenance, connectivity: ConnectivityStatus.Connected);
        device.Status = DeviceStatus.Maintenance;
        device.IsConnected = true;
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        // Simulate SignalR reconnect via the shared helper (same code path ClientHub uses).
        var tracked = await context.Devices.SingleAsync(d => d.Id == device.Id);
        Application.Common.DeviceStateTransitions.MarkClientConnected(tracked);
        await context.SaveChangesAsync();

        var reloaded = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.Equal(OperationalStatus.Maintenance, reloaded.OperationalStatus);
        Assert.Equal(DeviceStatus.Maintenance, reloaded.Status);
    }

    [Fact]
    public async Task Maintenance_MaintenanceRejectsNewSessionAndReservation()
    {
        using var context = CreateContext();
        var device = NewPc(operational: OperationalStatus.Maintenance);
        device.Status = DeviceStatus.Maintenance;
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var sessionResult = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.False(sessionResult.IsSuccess);

        var reservationResult = await CreateReservationHandler(context).HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), null, "Cashier", "Cashier"));
        Assert.False(reservationResult.IsSuccess);
    }

    // ===================== Deletion =====================

    [Fact]
    public async Task Delete_SoftDeletes_AndWritesAudit()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateDeleteHandler(context).HandleAsync(
            new DeleteDeviceCommand(device.Id, device.xmin, Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        var tracked = await context.Devices.SingleAsync(d => d.Id == device.Id);
        Assert.True(tracked.IsDeleted);
        Assert.False(tracked.IsActive);

        var audit = await context.AuditLogs.SingleAsync(a => a.Action == "DeleteDevice");
        Assert.Equal(device.Id, audit.EntityId);
    }

    [Fact]
    public async Task Delete_ActiveSessionBlocks()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        await SeedOpenSessionAsync(context, device);
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var result = await CreateDeleteHandler(context).HandleAsync(new DeleteDeviceCommand(device.Id, token));

        Assert.False(result.IsSuccess);
        Assert.Equal(DeviceLifecycleError.Conflict, result.ErrorKind);
        Assert.False((await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).IsDeleted);
    }

    [Fact]
    public async Task Delete_FutureReservationBlocks()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        await SeedFutureReservationAsync(context, device);
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var result = await CreateDeleteHandler(context).HandleAsync(new DeleteDeviceCommand(device.Id, token));

        Assert.False(result.IsSuccess);
        Assert.Equal(DeviceLifecycleError.Conflict, result.ErrorKind);
    }

    [Fact]
    public async Task Delete_StaleXminBlocks()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateDeleteHandler(context).HandleAsync(
            new DeleteDeviceCommand(device.Id, device.xmin + 42));

        Assert.False(result.IsSuccess);
        Assert.Equal(DeviceLifecycleError.StaleVersion, result.ErrorKind);
        Assert.False((await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).IsDeleted);
    }

    [Fact]
    public async Task Delete_MissingOrAlreadyDeleted_ReturnsNotFound()
    {
        using var context = CreateContext();
        var device = NewPc();
        device.IsDeleted = true;
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var result = await CreateDeleteHandler(context).HandleAsync(new DeleteDeviceCommand(device.Id, 0));
        Assert.Equal(DeviceLifecycleError.NotFound, result.ErrorKind);

        var missing = await CreateDeleteHandler(context).HandleAsync(new DeleteDeviceCommand(Guid.NewGuid(), 0));
        Assert.Equal(DeviceLifecycleError.NotFound, missing.ErrorKind);
    }

    [Fact]
    public async Task Delete_PreservesHistoricalSessionsReservationsAndAudit()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var session = await SeedOpenSessionAsync(context, device);
        var reservation = await SeedFutureReservationAsync(context, device);
        // End the session so deletion is allowed; keep the reservation cancelled.
        var trackedSession = await context.Sessions.SingleAsync(s => s.Id == session.Id);
        trackedSession.Status = SessionStatus.Completed;
        var trackedReservation = await context.Reservations.SingleAsync(r => r.Id == reservation.Id);
        trackedReservation.Status = ReservationStatus.Cancelled;
        var trackedDevice = await context.Devices.SingleAsync(d => d.Id == device.Id);
        trackedDevice.OperationalStatus = OperationalStatus.Available;
        trackedDevice.Status = DeviceStatus.Available;
        await context.SaveChangesAsync();
        var token = (await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id)).xmin;

        var result = await CreateDeleteHandler(context).HandleAsync(new DeleteDeviceCommand(device.Id, token));
        Assert.True(result.IsSuccess);

        // All historical rows preserved.
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(1, await context.Reservations.CountAsync());
        Assert.True(await context.AuditLogs.AnyAsync(a => a.Action == "DeleteDevice"));
    }

    // ===================== Zone hierarchy guards =====================

    private static async Task<Zone> SeedZoneAsync(CashierDbContext context, string name, Guid? parentId = null, bool isActive = true)
    {
        var zone = new Zone { Id = Guid.NewGuid(), Name = name, ParentId = parentId, IsActive = isActive, CreatedAt = DateTime.UtcNow };
        context.Zones.Add(zone);
        await context.SaveChangesAsync();
        return zone;
    }

    [Fact]
    public async Task Zone_InactiveDirectZone_BlocksSessionAndReservation()
    {
        using var context = CreateContext();
        var zone = await SeedZoneAsync(context, "Hall", isActive: false);
        var device = NewPc();
        device.ZoneId = zone.Id;
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var sessionResult = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.False(sessionResult.IsSuccess);

        var reservationResult = await CreateReservationHandler(context).HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), null, "Cashier", "Cashier"));
        Assert.False(reservationResult.IsSuccess);
    }

    [Fact]
    public async Task Zone_InactiveAncestorZone_BlocksSessionAndReservation()
    {
        using var context = CreateContext();
        var parent = await SeedZoneAsync(context, "Main Hall", isActive: false);
        var child = await SeedZoneAsync(context, "VIP Section", parent.Id);
        var device = NewPc();
        device.ZoneId = child.Id;
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var sessionResult = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.False(sessionResult.IsSuccess);

        var reservationResult = await CreateReservationHandler(context).HandleAsync(new CreateReservationCommand(
            customer.Id, device.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), null, "Cashier", "Cashier"));
        Assert.False(reservationResult.IsSuccess);
    }

    [Fact]
    public async Task Zone_DeletedAncestorZone_BlocksSession()
    {
        using var context = CreateContext();
        var parent = await SeedZoneAsync(context, "Main Hall");
        var child = await SeedZoneAsync(context, "VIP", parent.Id);
        // Soft-delete the ancestor.
        var trackedParent = await context.Zones.SingleAsync(z => z.Id == parent.Id);
        trackedParent.IsDeleted = true;
        await context.SaveChangesAsync();

        var device = NewPc();
        device.ZoneId = child.Id;
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var sessionResult = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.False(sessionResult.IsSuccess);
    }

    [Fact]
    public async Task Zone_ActiveAncestorChain_AllowsSession()
    {
        using var context = CreateContext();
        var parent = await SeedZoneAsync(context, "Main Hall");
        var child = await SeedZoneAsync(context, "VIP", parent.Id);
        var device = NewPc();
        device.ZoneId = child.Id;
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var sessionResult = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.True(sessionResult.IsSuccess);
    }

    // ===================== Session lifecycle synchronization =====================

    [Fact]
    public async Task Lifecycle_StartEnd_TransitionsOperationalStatusCorrectly()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var start = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, device.Id, null, "Cashier"));
        Assert.True(start.IsSuccess);
        var afterStart = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.Equal(OperationalStatus.InUse, afterStart.OperationalStatus);
        Assert.Equal(DeviceStatus.InUse, afterStart.Status);

        var billing = new Application.Services.SessionBillingService(
            new SessionRepository(context),
            new Application.Services.WalletService(new WalletRepository(context), new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context)),
            new UnitOfWork(context));
        var end = await new EndSessionHandler(
            new SessionRepository(context), new DeviceRepository(context), billing,
            new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context))
            .HandleAsync(new EndSessionCommand(start.Value!.SessionId, null, "Cashier"));
        Assert.True(end.IsSuccess);

        var afterEnd = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.Equal(OperationalStatus.Available, afterEnd.OperationalStatus);
        Assert.Equal(DeviceStatus.Available, afterEnd.Status);
        // Connectivity and activation untouched by session lifecycle.
        Assert.Equal(ConnectivityStatus.Disconnected, afterEnd.ConnectivityStatus);
        Assert.True(afterEnd.IsActive);
    }

    [Fact]
    public async Task Lifecycle_Transfer_UpdatesBothDevices()
    {
        using var context = CreateContext();
        var source = NewPc("PC-A");
        var target = NewPc("PC-B");
        context.Devices.AddRange(source, target);
        await context.SaveChangesAsync();
        var customer = await SeedCustomerAsync(context);
        await context.PricingRules.AddAsync(new PricingRule
        {
            Name = "PC hourly", DeviceType = DeviceType.GamingPC, PricePerHour = 1000m, IsActive = true
        });
        await context.SaveChangesAsync();

        var start = await CreateStartSessionHandler(context).HandleAsync(
            new StartSessionCommand(customer.Id, source.Id, null, "Cashier"));
        Assert.True(start.IsSuccess);

        var transfer = await new TransferSessionHandler(
            new SessionRepository(context), new DeviceRepository(context), new ZoneRepository(context),
            new AuditLogRepository(context), new OutboxRepository(context),
            new NoopCommandSender(), new UnitOfWork(context))
            .HandleAsync(new TransferSessionCommand(start.Value!.SessionId, target.Id, "Cashier"));

        Assert.True(transfer.IsSuccess);
        var sourceAfter = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == source.Id);
        var targetAfter = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == target.Id);
        Assert.Equal(OperationalStatus.Available, sourceAfter.OperationalStatus);
        Assert.Equal(DeviceStatus.Available, sourceAfter.Status);
        Assert.Equal(OperationalStatus.InUse, targetAfter.OperationalStatus);
        Assert.Equal(DeviceStatus.InUse, targetAfter.Status);
    }

    private sealed class NoopCommandSender : Application.Interfaces.IDeviceCommandSender
    {
        public Task<bool> SendAsync(Guid deviceId, Contracts.Hub.DeviceCommandMessage command, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    [Fact]
    public async Task Lifecycle_InactiveDeviceSessionEnds_DoesNotReactivate()
    {
        using var context = CreateContext();
        var device = NewPc();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var session = await SeedOpenSessionAsync(context, device);
        // Deactivate while session running.
        var tracked = await context.Devices.SingleAsync(d => d.Id == device.Id);
        tracked.IsActive = false;
        await context.SaveChangesAsync();

        var billing = new Application.Services.SessionBillingService(
            new SessionRepository(context),
            new Application.Services.WalletService(new WalletRepository(context), new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context)),
            new UnitOfWork(context));
        var end = await new EndSessionHandler(
            new SessionRepository(context), new DeviceRepository(context), billing,
            new AuditLogRepository(context), new OutboxRepository(context), new UnitOfWork(context))
            .HandleAsync(new EndSessionCommand(session.Id, null, "Cashier"));

        Assert.True(end.IsSuccess);
        var final = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.False(final.IsActive);
        Assert.Equal(OperationalStatus.Available, final.OperationalStatus);
    }

    [Fact]
    public async Task Lifecycle_HeartbeatDisconnect_DoesNotAffectOperationalState()
    {
        using var context = CreateContext();
        var device = NewPc(operational: OperationalStatus.InUse, connectivity: ConnectivityStatus.Connected);
        device.Status = DeviceStatus.InUse;
        device.IsConnected = true;
        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var tracked = await context.Devices.SingleAsync(d => d.Id == device.Id);
        Application.Common.DeviceStateTransitions.MarkClientDisconnected(tracked);
        await context.SaveChangesAsync();

        var final = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.Equal(OperationalStatus.InUse, final.OperationalStatus);
        Assert.Equal(DeviceStatus.InUse, final.Status);
        Assert.Equal(ConnectivityStatus.Disconnected, final.ConnectivityStatus);
    }

    [Fact]
    public async Task SessionRecovery_ReconcilesOperationalState_PreservingMaintenance()
    {
        using var context = CreateContext();
        var inUseNoSession = NewPc("PC-FREE", operational: OperationalStatus.InUse);
        inUseNoSession.Status = DeviceStatus.InUse;
        var maintenanceDevice = NewPc("PC-MAINT", operational: OperationalStatus.Maintenance);
        maintenanceDevice.Status = DeviceStatus.Maintenance;
        context.Devices.AddRange(inUseNoSession, maintenanceDevice);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton<ISessionRepository>(new SessionRepository(context));
        services.AddSingleton<IDeviceRepository>(new DeviceRepository(context));
        await using var provider = services.BuildServiceProvider();

        var recovery = new Infrastructure.Hosting.SessionRecoveryService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.Hosting.SessionRecoveryService>.Instance);

        await recovery.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await recovery.StopAsync(CancellationToken.None);

        var free = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == inUseNoSession.Id);
        Assert.Equal(OperationalStatus.Available, free.OperationalStatus);
        Assert.Equal(DeviceStatus.Available, free.Status);

        var maint = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == maintenanceDevice.Id);
        Assert.Equal(OperationalStatus.Maintenance, maint.OperationalStatus);
        Assert.Equal(DeviceStatus.Maintenance, maint.Status);
    }
}
