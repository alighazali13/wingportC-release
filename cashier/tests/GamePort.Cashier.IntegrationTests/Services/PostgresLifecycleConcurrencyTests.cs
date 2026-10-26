using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// C2-C mandatory PostgreSQL tests: real EF Core xmin concurrency for activation/
/// operational-status PATCH and DELETE handlers, plus parallel heartbeat vs
/// operational-update safety. Uses the disposable-schema isolation from C2-B.
/// </summary>
public class PostgresLifecycleConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public PostgresLifecycleConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private async Task<PostgresTestDatabase> CreateOrFailAsync()
    {
        var db = await PostgresTestDatabase.CreateAsync();
        if (!db.IsAvailable)
        {
            _output.WriteLine("PostgreSQL is NOT reachable. Lifecycle concurrency cannot be verified.");
            Assert.Fail("PostgreSQL required for lifecycle xmin concurrency verification. Set WINGPORT_TEST_DB or make the local development database reachable.");
        }

        await db.EnsureSchemaCreatedAsync();
        return db;
    }

    private static Device NewPc(string name = "PC-LIFE") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Type = DeviceType.GamingPC,
        Status = DeviceStatus.Available,
        IpAddress = "10.88.0.10",
        ConnectionMode = ConnectionMode.Client,
        ConnectivityStatus = ConnectivityStatus.Disconnected,
        OperationalStatus = OperationalStatus.Available,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static SetDeviceActivationHandler CreateActivationHandler(CashierDbContext context) =>
        new(new DeviceRepository(context), new AuditLogRepository(context), new UnitOfWork(context));

    private static SetDeviceOperationalStatusHandler CreateOpsHandler(CashierDbContext context) =>
        new(new DeviceRepository(context), new SessionRepository(context), new AuditLogRepository(context), new UnitOfWork(context));

    private static DeleteDeviceHandler CreateDeleteHandler(CashierDbContext context) =>
        new(new DeviceRepository(context), new SessionRepository(context), new ReservationRepository(context),
            new AuditLogRepository(context), new UnitOfWork(context));

    [Fact]
    public async Task Activation_StaleXmin_Returns409Semantics_AndFreshTokenSucceeds()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        uint token;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-ACT");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
            token = device.xmin;
        }

        // Concurrent writer bumps xmin.
        await using (var writer = db.CreateContext())
        {
            var d = await writer.Devices.SingleAsync(x => x.Id == deviceId);
            d.MaintenanceNotes = "concurrent";
            await writer.SaveChangesAsync();
        }

        // Stale token → StaleVersion (HTTP 409 at endpoint).
        await using (var staleCtx = db.CreateContext())
        {
            var stale = await CreateActivationHandler(staleCtx).HandleAsync(
                new SetDeviceActivationCommand(deviceId, false, token));
            Assert.False(stale.IsSuccess);
            Assert.Equal(DeviceLifecycleError.StaleVersion, stale.ErrorKind);
        }

        // Fresh token → success, refreshed xmin returned.
        await using (var freshCtx = db.CreateContext())
        {
            var current = await freshCtx.Devices.AsNoTracking().SingleAsync(x => x.Id == deviceId);
            var fresh = await CreateActivationHandler(freshCtx).HandleAsync(
                new SetDeviceActivationCommand(deviceId, false, current.xmin));
            Assert.True(fresh.IsSuccess);
            Assert.False(fresh.Device!.IsActive);
            Assert.NotEqual(current.xmin, fresh.Device.xmin);
        }
    }

    [Fact]
    public async Task Activation_ConcurrentSaveDuringTransaction_ThrowsConcurrency()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-ACT2");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
        }

        await using var handlerCtx = db.CreateContext();
        var loaded = await handlerCtx.Devices.SingleAsync(x => x.Id == deviceId);

        await using (var writer = db.CreateContext())
        {
            var other = await writer.Devices.SingleAsync(x => x.Id == deviceId);
            other.MaintenanceNotes = "won the race";
            await writer.SaveChangesAsync();
        }

        var repository = new DeviceRepository(handlerCtx);
        loaded.IsActive = false;
        await Assert.ThrowsAsync<Domain.Exceptions.ConcurrencyConflictException>(() => repository.UpdateAsync(loaded));
    }

    [Fact]
    public async Task OperationalStatus_StaleXmin_StaleVersion_FreshSucceeds_WithRefreshedXmin()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        uint token;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-OPS");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
            token = device.xmin;
        }

        await using (var writer = db.CreateContext())
        {
            var d = await writer.Devices.SingleAsync(x => x.Id == deviceId);
            d.MaintenanceNotes = "bump";
            await writer.SaveChangesAsync();
        }

        await using (var staleCtx = db.CreateContext())
        {
            var stale = await CreateOpsHandler(staleCtx).HandleAsync(new SetDeviceOperationalStatusCommand(
                deviceId, OperationalStatus.Maintenance, "to maintenance", token));
            Assert.False(stale.IsSuccess);
            Assert.Equal(DeviceLifecycleError.StaleVersion, stale.ErrorKind);
        }

        await using (var freshCtx = db.CreateContext())
        {
            var current = await freshCtx.Devices.AsNoTracking().SingleAsync(x => x.Id == deviceId);
            var fresh = await CreateOpsHandler(freshCtx).HandleAsync(new SetDeviceOperationalStatusCommand(
                deviceId, OperationalStatus.Maintenance, "to maintenance", current.xmin));
            Assert.True(fresh.IsSuccess);
            Assert.Equal(OperationalStatus.Maintenance, fresh.Device!.OperationalStatus);
            Assert.NotEqual(current.xmin, fresh.Device.xmin);
        }
    }

    [Fact]
    public async Task Delete_StaleXmin_StaleVersion_FreshSucceeds_SoftDeleteOnly()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        uint token;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-DEL");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
            token = device.xmin;
        }

        await using (var writer = db.CreateContext())
        {
            var d = await writer.Devices.SingleAsync(x => x.Id == deviceId);
            d.MaintenanceNotes = "bump";
            await writer.SaveChangesAsync();
        }

        await using (var staleCtx = db.CreateContext())
        {
            var stale = await CreateDeleteHandler(staleCtx).HandleAsync(new DeleteDeviceCommand(deviceId, token));
            Assert.False(stale.IsSuccess);
            Assert.Equal(DeviceLifecycleError.StaleVersion, stale.ErrorKind);
        }

        await using (var freshCtx = db.CreateContext())
        {
            var current = await freshCtx.Devices.AsNoTracking().SingleAsync(x => x.Id == deviceId);
            var fresh = await CreateDeleteHandler(freshCtx).HandleAsync(new DeleteDeviceCommand(deviceId, current.xmin));
            Assert.True(fresh.IsSuccess);
        }

        // Soft delete: the row still exists; it is only flagged.
        await using (var verify = db.CreateContext())
        {
            var row = await verify.Devices.AsNoTracking().SingleAsync(x => x.Id == deviceId);
            Assert.True(row.IsDeleted);
            Assert.Equal("PC-DEL", row.Name);
        }

        // GetByIdAsync (used by GET endpoints) must not return the deleted device.
        await using (var getCtx = db.CreateContext())
        {
            Assert.Null(await new DeviceRepository(getCtx).GetByIdAsync(deviceId));
        }
    }

    [Fact]
    public async Task Delete_RowPhysicallyRemoved_Never()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-SOFT");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
        }

        await using (var del = db.CreateContext())
        {
            var current = await del.Devices.AsNoTracking().SingleAsync(x => x.Id == deviceId);
            var result = await CreateDeleteHandler(del).HandleAsync(new DeleteDeviceCommand(deviceId, current.xmin));
            Assert.True(result.IsSuccess);
        }

        await using (var raw = db.CreateContext())
        {
            // Count via tracking query without the IsDeleted filter.
            var count = await raw.Devices.IgnoreQueryFilters().CountAsync(x => x.Id == deviceId);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task ParallelHeartbeatAndOperationalUpdate_BothApply_WithoutLostUpdate()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-PAR");
            device.IsConnected = true;
            device.ConnectivityStatus = ConnectivityStatus.Connected;
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
        }

        // Context A: heartbeat/disconnect path (connectivity only).
        // Context B: operational maintenance path. Both load the same starting version
        // and race. EF + xmin guarantees one wins; the loser gets ConcurrencyConflictException
        // and the caller retries with a fresh token (the handler pre-check does this at API level).
        await using var heartbeatCtx = db.CreateContext();
        await using var opsCtx = db.CreateContext();

        var heartbeatDevice = await heartbeatCtx.Devices.SingleAsync(x => x.Id == deviceId);
        var opsDevice = await opsCtx.Devices.SingleAsync(x => x.Id == deviceId);
        Assert.Equal(heartbeatDevice.xmin, opsDevice.xmin);

        // Heartbeat applies first.
        DeviceStateTransitions.MarkClientDisconnected(heartbeatDevice);
        heartbeatDevice.IsConnected = false;
        heartbeatDevice.LastHeartbeat = DateTime.UtcNow;
        await new DeviceRepository(heartbeatCtx).UpdateAsync(heartbeatDevice);

        // Operational update with the now-stale version must fail with concurrency.
        DeviceStateTransitions.MarkMaintenanceEntered(opsDevice);
        opsDevice.LastMaintenanceAt = DateTime.UtcNow;
        var opsRepository = new DeviceRepository(opsCtx);
        await Assert.ThrowsAsync<Domain.Exceptions.ConcurrencyConflictException>(() => opsRepository.UpdateAsync(opsDevice));

        // Retry with fresh version — the standard recovery path used by the PATCH endpoint.
        await using var retryCtx = db.CreateContext();
        var fresh = await retryCtx.Devices.SingleAsync(x => x.Id == deviceId);
        Assert.Equal(ConnectivityStatus.Disconnected, fresh.ConnectivityStatus);
        Assert.Equal(OperationalStatus.Available, fresh.OperationalStatus);

        var retryOps = new SetDeviceOperationalStatusHandler(
            new DeviceRepository(retryCtx), new SessionRepository(retryCtx),
            new AuditLogRepository(retryCtx), new UnitOfWork(retryCtx));
        var retryResult = await retryOps.HandleAsync(new SetDeviceOperationalStatusCommand(
            deviceId, OperationalStatus.Maintenance, "after retry", fresh.xmin));
        Assert.True(retryResult.IsSuccess);

        // Final state: both changes are present — connectivity from heartbeat,
        // operational from the maintenance PATCH. No lost update.
        await using var finalCtx = db.CreateContext();
        var final = await finalCtx.Devices.AsNoTracking().SingleAsync(x => x.Id == deviceId);
        Assert.Equal(ConnectivityStatus.Disconnected, final.ConnectivityStatus);
        Assert.False(final.IsConnected);
        Assert.Equal(OperationalStatus.Maintenance, final.OperationalStatus);
        Assert.Equal(DeviceStatus.Maintenance, final.Status);
    }

    [Fact]
    public async Task ParallelHeartbeat_ConnectivityOnlyUpdate_DoesNotBumpOperational()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-HB");
            device.IsConnected = true;
            device.ConnectivityStatus = ConnectivityStatus.Connected;
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
        }

        // Simulate several heartbeat updates in sequence (as the hub does).
        for (var i = 0; i < 3; i++)
        {
            await using var ctx = db.CreateContext();
            var d = await ctx.Devices.SingleAsync(x => x.Id == deviceId);
            var operationalBefore = d.OperationalStatus;
            d.IsConnected = true;
            d.LastHeartbeat = DateTime.UtcNow;
            DeviceStateTransitions.MarkClientConnected(d);
            await new DeviceRepository(ctx).UpdateAsync(d);
            Assert.Equal(operationalBefore, d.OperationalStatus);
        }
    }
}
