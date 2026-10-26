using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// C2-B registration behavior tests: GamingPC, PlayStation, reference validation, audit logging.
/// </summary>
public class DeviceRegistrationHandlerTests
{
    private static CashierDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseInMemoryDatabase($"wingport-reg-{Guid.NewGuid():N}")
            .Options;
        return new CashierDbContext(options);
    }

    private static RegisterDeviceHandler CreateHandler(CashierDbContext context) =>
        new(
            new DeviceRepository(context),
            new ZoneRepository(context),
            new PricingGroupRepository(context),
            new AuditLogRepository(context),
            new OutboxRepository(context),
            new Pbkdf2SecretHasher(),
            new UnitOfWork(context));

    private static async Task<Zone> SeedZoneAsync(CashierDbContext context, bool isActive = true)
    {
        var zone = new Zone { Name = "Main Hall", IsActive = isActive, DisplayOrder = 1 };
        context.Zones.Add(zone);
        await context.SaveChangesAsync();
        return zone;
    }

    private static async Task<PricingGroup> SeedPricingGroupAsync(
        CashierDbContext context, DeviceType? deviceType = null, bool isActive = true)
    {
        var group = new PricingGroup { Name = "Standard", DeviceType = deviceType, IsActive = isActive };
        context.PricingGroups.Add(group);
        await context.SaveChangesAsync();
        return group;
    }

    [Fact]
    public async Task Register_GamingPC_SetsClientModeDisconnectedAndCredentials()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-10", DeviceType.GamingPC, "192.168.1.110", "AA:BB:CC:DD:EE:FF", "GPU:RTX4090"));

        Assert.True(result.IsSuccess);
        var device = result.Value!.Device;
        Assert.Equal(DeviceType.GamingPC, device.Type);
        Assert.Equal(DeviceStatus.Available, device.Status);
        Assert.Equal(OperationalStatus.Available, device.OperationalStatus);
        Assert.Equal(ConnectivityStatus.Disconnected, device.ConnectivityStatus);
        Assert.Equal(ConnectionMode.Client, device.ConnectionMode);
        Assert.True(device.IsActive);
        Assert.False(device.IsConnected);
        Assert.False(string.IsNullOrEmpty(device.ClientIdentity));
        Assert.False(string.IsNullOrEmpty(device.ClientSecretHash));
        Assert.NotEqual(result.Value.ClientSecret, device.ClientSecretHash);
        Assert.True(new Pbkdf2SecretHasher().Verify(result.Value.ClientSecret, device.ClientSecretHash!));
        Assert.NotNull(device.CredentialIssuedAt);
    }

    [Fact]
    public async Task Register_PlayStation_SetsManualModeWithoutCredentials()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PS5-01", DeviceType.PlayStation, null, null, null,
            ConsoleModel: "Slim", ControllerCount: 2, MaxPlayers: 4));

        Assert.True(result.IsSuccess);
        var device = result.Value!.Device;
        Assert.Equal(ConnectionMode.Manual, device.ConnectionMode);
        Assert.Equal(ConnectivityStatus.NotApplicable, device.ConnectivityStatus);
        Assert.Equal(OperationalStatus.Available, device.OperationalStatus);
        Assert.True(device.IsActive);
        Assert.Null(device.ClientIdentity);
        Assert.Null(device.ClientSecretHash);
        Assert.Null(device.CredentialIssuedAt);
        Assert.Equal(string.Empty, result.Value.ClientSecret);
        Assert.Equal("Slim", device.ConsoleModel);
        Assert.Equal(2, device.ControllerCount);
        Assert.Equal(4, device.MaxPlayers);
        Assert.Null(device.IpAddress);
    }

    [Fact]
    public async Task Register_GamingPC_WithoutIp_Fails()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-X", DeviceType.GamingPC, null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Devices.CountAsync());
    }

    [Fact]
    public async Task Register_GamingPC_WithInvalidIp_Fails()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-X", DeviceType.GamingPC, "not-an-ip", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Devices.CountAsync());
    }

    [Fact]
    public async Task Register_PlayStation_WithInvalidIp_Fails()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PS-X", DeviceType.PlayStation, "999.999.999.999", null, null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Register_GamingPC_WithConsoleFields_Fails()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-X", DeviceType.GamingPC, "10.0.0.9", null, null,
            ConsoleModel: "Slim"));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Devices.CountAsync());
    }

    [Fact]
    public async Task Register_InvalidZoneId_Fails()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-Z", DeviceType.GamingPC, "10.0.0.10", null, null,
            ZoneId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await context.Devices.CountAsync());
    }

    [Fact]
    public async Task Register_InactiveZone_Fails()
    {
        using var context = CreateContext();
        var zone = await SeedZoneAsync(context, isActive: false);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-Z", DeviceType.GamingPC, "10.0.0.11", null, null,
            ZoneId: zone.Id));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Register_ValidZone_Succeeds()
    {
        using var context = CreateContext();
        var zone = await SeedZoneAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-Z", DeviceType.GamingPC, "10.0.0.12", null, null,
            ZoneId: zone.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(zone.Id, result.Value!.Device.ZoneId);
    }

    [Fact]
    public async Task Register_InvalidPricingGroupId_Fails()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-P", DeviceType.GamingPC, "10.0.0.13", null, null,
            PricingGroupId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Register_InactivePricingGroup_Fails()
    {
        using var context = CreateContext();
        var group = await SeedPricingGroupAsync(context, isActive: false);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-P", DeviceType.GamingPC, "10.0.0.14", null, null,
            PricingGroupId: group.Id));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Register_PricingGroupDeviceTypeMismatch_Fails()
    {
        using var context = CreateContext();
        var pcGroup = await SeedPricingGroupAsync(context, DeviceType.GamingPC);
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PS-P", DeviceType.PlayStation, null, null, null,
            PricingGroupId: pcGroup.Id));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Register_PricingGroupWithNullDeviceType_AcceptsBothTypes()
    {
        using var context = CreateContext();
        var anyGroup = await SeedPricingGroupAsync(context, deviceType: null);
        var handler = CreateHandler(context);

        var pcResult = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-G", DeviceType.GamingPC, "10.0.0.15", null, null,
            PricingGroupId: anyGroup.Id));
        var psResult = await handler.HandleAsync(new RegisterDeviceCommand(
            "PS-G", DeviceType.PlayStation, null, null, null,
            PricingGroupId: anyGroup.Id));

        Assert.True(pcResult.IsSuccess);
        Assert.True(psResult.IsSuccess);
    }

    [Fact]
    public async Task Register_NullReferences_AreAllowed()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-N", DeviceType.GamingPC, "10.0.0.16", null, null,
            ZoneId: null, PricingGroupId: null));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Device.ZoneId);
        Assert.Null(result.Value.Device.PricingGroupId);
    }

    [Fact]
    public async Task Register_WritesAuditLog_WithoutSecrets()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-A", DeviceType.GamingPC, "10.0.0.17", null, null,
            ActorEmployeeId: Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        var audit = await context.AuditLogs.SingleAsync(a => a.Action == "RegisterDevice");
        Assert.Equal(result.Value!.Device.Id, audit.EntityId);
        Assert.Equal(AuditActorType.Employee, audit.ActorType);
        Assert.NotNull(result.Value.Device.ClientSecretHash);
        Assert.DoesNotContain(result.Value.ClientSecret, audit.AfterState ?? string.Empty);
        Assert.DoesNotContain(result.Value.Device.ClientSecretHash, audit.AfterState ?? string.Empty);
    }

    [Fact]
    public async Task Register_PlayStation_WithValidIp_Succeeds()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PS5-IP", DeviceType.PlayStation, "192.168.1.200", null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("192.168.1.200", result.Value!.Device.IpAddress);
    }

    [Fact]
    public async Task Register_DisplayOrder_IsPersisted()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(new RegisterDeviceCommand(
            "PC-DO", DeviceType.GamingPC, "10.0.0.18", null, null, DisplayOrder: 7));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.Device.DisplayOrder);
    }
}
