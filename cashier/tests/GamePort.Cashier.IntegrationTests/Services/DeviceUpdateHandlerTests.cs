using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// C2-B PUT device behavior tests: permitted changes, immutable protection,
/// stale xmin detection, reference validation, audit logging.
/// Note: xmin on InMemory is always 0 (no PostgreSQL system column) — stale-token
/// semantics are additionally verified against real PostgreSQL in PostgresXminConcurrencyTests.
/// </summary>
public class DeviceUpdateHandlerTests
{
    private static CashierDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseInMemoryDatabase($"wingport-upd-{Guid.NewGuid():N}")
            .Options;
        return new CashierDbContext(options);
    }

    private static UpdateDeviceHandler CreateHandler(CashierDbContext context) =>
        new(
            new DeviceRepository(context),
            new ZoneRepository(context),
            new PricingGroupRepository(context),
            new AuditLogRepository(context),
            new UnitOfWork(context));

    private static async Task<Device> SeedPcAsync(CashierDbContext context)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "PC-01",
            Type = DeviceType.GamingPC,
            Status = DeviceStatus.Available,
            IpAddress = "192.168.1.101",
            ClientIdentity = "dev-test-001",
            ClientSecretHash = "hash-test",
            ConnectionMode = ConnectionMode.Client,
            ConnectivityStatus = ConnectivityStatus.Disconnected,
            OperationalStatus = OperationalStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static UpdateDeviceCommand BaseCommand(Device device, Action<UpdateDeviceBuilder>? configure = null, uint xmin = 0)
    {
        var builder = new UpdateDeviceBuilder(device, xmin);
        configure?.Invoke(builder);
        return builder.Build();
    }

    private sealed class UpdateDeviceBuilder
    {
        private readonly Device _device;
        private uint _xmin;
        private string _name;
        private string? _ip;
        private string? _mac;
        private Guid? _zoneId;
        private Guid? _pricingGroupId;
        private int _displayOrder;
        private string? _notes;
        private string? _consoleModel;
        private int? _controllers;
        private int? _maxPlayers;
        private Guid? _actor;

        public UpdateDeviceBuilder(Device device, uint xmin)
        {
            _device = device;
            _xmin = xmin;
            _name = device.Name;
            _ip = device.IpAddress;
            _mac = device.MacAddress;
            _zoneId = device.ZoneId;
            _pricingGroupId = device.PricingGroupId;
            _displayOrder = device.DisplayOrder;
            _notes = device.MaintenanceNotes;
            _consoleModel = device.ConsoleModel;
            _controllers = device.ControllerCount;
            _maxPlayers = device.MaxPlayers;
        }

        public UpdateDeviceBuilder WithName(string name) { _name = name; return this; }
        public UpdateDeviceBuilder WithIp(string? ip) { _ip = ip; return this; }
        public UpdateDeviceBuilder WithZone(Guid? zoneId) { _zoneId = zoneId; return this; }
        public UpdateDeviceBuilder WithPricingGroup(Guid? pgId) { _pricingGroupId = pgId; return this; }
        public UpdateDeviceBuilder WithDisplayOrder(int order) { _displayOrder = order; return this; }
        public UpdateDeviceBuilder WithNotes(string? notes) { _notes = notes; return this; }
        public UpdateDeviceBuilder WithConsole(string? model, int? controllers = null, int? maxPlayers = null)
        { _consoleModel = model; _controllers = controllers; _maxPlayers = maxPlayers; return this; }
        public UpdateDeviceBuilder WithActor(Guid? actor) { _actor = actor; return this; }

        public UpdateDeviceCommand Build() => new(
            _device.Id, _name, _ip, _mac, _zoneId, _pricingGroupId, _displayOrder,
            _notes, _consoleModel, _controllers, _maxPlayers, _xmin, _actor);
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesPermittedFields_AndReturnsEntity()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b
            .WithName("PC-01 Renamed")
            .WithIp("192.168.1.150")
            .WithDisplayOrder(5)
            .WithNotes("cleaned")));

        Assert.True(result.IsSuccess);
        Assert.Equal("PC-01 Renamed", result.Device!.Name);
        Assert.Equal("192.168.1.150", result.Device.IpAddress);
        Assert.Equal(5, result.Device.DisplayOrder);
        Assert.Equal("cleaned", result.Device.MaintenanceNotes);

        var persisted = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.Equal("PC-01 Renamed", persisted.Name);
        Assert.Equal("192.168.1.150", persisted.IpAddress);
    }

    [Fact]
    public async Task Update_ProtectsImmutableFields()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device));

        Assert.True(result.IsSuccess);
        var updated = result.Device!;
        Assert.Equal(device.Id, updated.Id);
        Assert.Equal(DeviceType.GamingPC, updated.Type);
        Assert.Equal("dev-test-001", updated.ClientIdentity);
        Assert.Equal("hash-test", updated.ClientSecretHash);
        Assert.Equal(ConnectionMode.Client, updated.ConnectionMode);
        Assert.True(updated.IsActive);
        Assert.Equal(OperationalStatus.Available, updated.OperationalStatus);
        Assert.Equal(ConnectivityStatus.Disconnected, updated.ConnectivityStatus);
        Assert.Equal(DeviceStatus.Available, updated.Status);
        Assert.False(updated.IsConnected);
        Assert.Null(updated.LastHeartbeat);
        Assert.Null(updated.CurrentGameId);
    }

    [Fact]
    public async Task Update_MissingDevice_ReturnsNotFound()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new UpdateDeviceCommand(
            Guid.NewGuid(), "X", "1.2.3.4", null, null, null, 0, null, null, null, null, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task Update_StaleXminToken_ReturnsStaleVersion_WithoutSaving()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        // Token 999 never matches the database version (0 on InMemory / actual xmin on PG).
        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithName("ShouldNotApply"), xmin: 999));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.StaleVersion, result.ErrorKind);
        var persisted = await context.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.Equal("PC-01", persisted.Name);
    }

    [Fact]
    public async Task Update_GamingPC_WithoutIp_Fails()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithIp(null)));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_GamingPC_WithInvalidIp_Fails()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithIp("not-an-ip")));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_GamingPC_WithConsoleFields_Fails()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithConsole("Slim")));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_PlayStation_AllowsNullIpAndConsoleFields()
    {
        using var context = CreateContext();
        var ps = new Device
        {
            Id = Guid.NewGuid(),
            Name = "PS5-01",
            Type = DeviceType.PlayStation,
            Status = DeviceStatus.Available,
            ConnectionMode = ConnectionMode.Manual,
            ConnectivityStatus = ConnectivityStatus.NotApplicable,
            OperationalStatus = OperationalStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Devices.Add(ps);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(ps, b => b
            .WithIp(null)
            .WithConsole("Pro", 4, 8)));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Device!.IpAddress);
        Assert.Equal("Pro", result.Device.ConsoleModel);
        Assert.Equal(4, result.Device.ControllerCount);
        Assert.Equal(8, result.Device.MaxPlayers);
    }

    [Fact]
    public async Task Update_InvalidZoneId_Fails()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithZone(Guid.NewGuid())));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_PricingGroupDeviceTypeMismatch_Fails()
    {
        using var context = CreateContext();
        var pcGroup = new PricingGroup { Name = "PC Only", DeviceType = DeviceType.GamingPC, IsActive = true };
        context.PricingGroups.Add(pcGroup);
        var ps = new Device
        {
            Id = Guid.NewGuid(),
            Name = "PS5-01",
            Type = DeviceType.PlayStation,
            Status = DeviceStatus.Available,
            ConnectionMode = ConnectionMode.Manual,
            ConnectivityStatus = ConnectivityStatus.NotApplicable,
            OperationalStatus = OperationalStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Devices.Add(ps);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(ps, b => b.WithPricingGroup(pcGroup.Id)));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_ClearingReferences_IsAllowed()
    {
        using var context = CreateContext();
        var zone = new Zone { Name = "Hall", IsActive = true };
        context.Zones.Add(zone);
        var device = await SeedPcAsync(context);
        device.ZoneId = zone.Id;
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithZone(null)));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Device!.ZoneId);
    }

    [Fact]
    public async Task Update_WritesAuditLog_WithBeforeAndAfterState()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b
            .WithName("PC-02")
            .WithActor(Guid.NewGuid())));

        Assert.True(result.IsSuccess);
        var audit = await context.AuditLogs.SingleAsync(a => a.Action == "UpdateDevice");
        Assert.Equal(device.Id, audit.EntityId);
        Assert.Equal(AuditActorType.Employee, audit.ActorType);
        Assert.NotNull(audit.BeforeState);
        Assert.NotNull(audit.AfterState);
        Assert.Contains("PC-01", audit.BeforeState!);
        Assert.Contains("PC-02", audit.AfterState!);
        Assert.DoesNotContain("hash-test", audit.AfterState!);
    }

    [Fact]
    public async Task Update_DoesNotTouchSessionsOrReservations()
    {
        using var context = CreateContext();
        var device = await SeedPcAsync(context);
        var customer = new Customer { Name = "Ali", PhoneNumber = "09120000001", CreatedAt = DateTime.UtcNow };
        context.Customers.Add(customer);
        context.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = DeviceType.GamingPC,
            StartTime = DateTime.UtcNow,
            PriceAtStart = 1000m,
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = DeviceType.GamingPC,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ReservationStatus.Reserved,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(BaseCommand(device, b => b.WithName("PC-Updated")));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(1, await context.Reservations.CountAsync());
        var session = await context.Sessions.SingleAsync();
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(device.Id, session.DeviceId);
    }
}
