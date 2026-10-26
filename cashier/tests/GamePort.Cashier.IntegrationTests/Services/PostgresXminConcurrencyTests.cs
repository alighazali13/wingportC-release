using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Domain.Exceptions;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// Mandatory C2-B PostgreSQL xmin optimistic-concurrency tests.
/// Runs against a disposable schema (Npgsql SearchPath isolation) on the local
/// PostgreSQL — never against the public schema or PC-01. Fails loudly when
/// PostgreSQL is unreachable so concurrency is never silently "verified".
/// </summary>
public class PostgresXminConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public PostgresXminConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private async Task<PostgresTestDatabase> CreateOrFailAsync()
    {
        var db = await PostgresTestDatabase.CreateAsync();
        if (!db.IsAvailable)
        {
            _output.WriteLine("PostgreSQL is NOT reachable. xmin concurrency cannot be verified.");
            Assert.Fail("PostgreSQL required for xmin concurrency verification. Set WINGPORT_TEST_DB or make the local development database reachable.");
        }

        await db.EnsureSchemaCreatedAsync();
        return db;
    }

    private static Device NewPc(string name = "PC-CONC") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Type = DeviceType.GamingPC,
        Status = DeviceStatus.Available,
        IpAddress = "10.99.0.10",
        ConnectionMode = ConnectionMode.Client,
        ConnectivityStatus = ConnectivityStatus.Disconnected,
        OperationalStatus = OperationalStatus.Available,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task TwoContexts_StaleSave_ThrowsDbUpdateConcurrencyException()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc();
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
        }

        // Context A and B load the same device independently.
        await using var contextA = db.CreateContext();
        await using var contextB = db.CreateContext();

        var deviceA = await contextA.Devices.SingleAsync(d => d.Id == deviceId);
        var deviceB = await contextB.Devices.SingleAsync(d => d.Id == deviceId);

        Assert.Equal(deviceA.xmin, deviceB.xmin);
        var originalXmin = deviceA.xmin;

        // Context A updates successfully — PostgreSQL bumps xmin.
        deviceA.Name = "PC-CONC-A";
        await contextA.SaveChangesAsync();
        Assert.NotEqual(originalXmin, deviceA.xmin);

        // Context B attempts a stale save — EF must throw DbUpdateConcurrencyException.
        deviceB.Name = "PC-CONC-B";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());

        // A's change is preserved.
        await using var verify = db.CreateContext();
        var final = await verify.Devices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        Assert.Equal("PC-CONC-A", final.Name);
    }

    [Fact]
    public async Task UpdateDeviceHandler_StaleXmin_ReturnsStaleVersion()
    {
        await using var db = await CreateOrFailAsync();

        uint staleToken;
        Guid deviceId;

        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-HANDLER");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
            staleToken = device.xmin;
        }

        // Another writer bumps xmin after the client read its token.
        await using (var writer = db.CreateContext())
        {
            var device = await writer.Devices.SingleAsync(d => d.Id == deviceId);
            device.MaintenanceNotes = "touched by someone else";
            await writer.SaveChangesAsync();
        }

        await using var handlerContext = db.CreateContext();
        var handler = new UpdateDeviceHandler(
            new DeviceRepository(handlerContext),
            new ZoneRepository(handlerContext),
            new PricingGroupRepository(handlerContext),
            new AuditLogRepository(handlerContext),
            new UnitOfWork(handlerContext));

        var result = await handler.HandleAsync(new UpdateDeviceCommand(
            deviceId, "PC-HANDLER-RENAMED", "10.99.0.11", null, null, null, 0,
            null, null, null, null, staleToken));

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateDeviceError.StaleVersion, result.ErrorKind);

        await using var verify = db.CreateContext();
        var final = await verify.Devices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        Assert.Equal("PC-HANDLER", final.Name);
    }

    [Fact]
    public async Task UpdateDeviceHandler_FreshXmin_Succeeds_AndReturnsRefreshedXmin()
    {
        await using var db = await CreateOrFailAsync();

        uint token;
        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-FRESH");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
            token = device.xmin;
        }

        await using var handlerContext = db.CreateContext();
        var handler = new UpdateDeviceHandler(
            new DeviceRepository(handlerContext),
            new ZoneRepository(handlerContext),
            new PricingGroupRepository(handlerContext),
            new AuditLogRepository(handlerContext),
            new UnitOfWork(handlerContext));

        var result = await handler.HandleAsync(new UpdateDeviceCommand(
            deviceId, "PC-FRESH-2", "10.99.0.20", null, null, null, 3,
            "notes", null, null, null, token));

        Assert.True(result.IsSuccess);
        Assert.Equal("PC-FRESH-2", result.Device!.Name);
        Assert.Equal(3, result.Device.DisplayOrder);
        Assert.NotEqual(token, result.Device.xmin);
        Assert.True(result.Device.xmin > 0);

        // Capture the refreshed token BEFORE the second call — both handler calls share
        // one scoped context, so GetByIdAsync returns the same tracked instance and
        // result.Device/second.Device are reference-equal after the second save.
        var refreshedToken = result.Device.xmin;

        // A second update with the refreshed token must also succeed.
        var second = await handler.HandleAsync(new UpdateDeviceCommand(
            deviceId, "PC-FRESH-3", "10.99.0.21", null, null, null, 3,
            null, null, null, null, refreshedToken));

        Assert.True(second.IsSuccess);
        Assert.NotEqual(refreshedToken, second.Device!.xmin);

        await using var verify = db.CreateContext();
        var final = await verify.Devices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        Assert.Equal("PC-FRESH-3", final.Name);
        Assert.Equal(second.Device.xmin, final.xmin);
    }

    [Fact]
    public async Task RepositoryUpdate_ConcurrentCommitBetweenLoadAndSave_ThrowsConcurrencyConflict()
    {
        await using var db = await CreateOrFailAsync();

        Guid deviceId;
        await using (var seed = db.CreateContext())
        {
            var device = NewPc("PC-RACE");
            seed.Devices.Add(device);
            await seed.SaveChangesAsync();
            deviceId = device.Id;
        }

        // Handler context loads the device (xmin captured as OriginalValue).
        await using var handlerContext = db.CreateContext();
        var loaded = await handlerContext.Devices.SingleAsync(d => d.Id == deviceId);

        // A concurrent writer commits BEFORE the handler saves.
        await using (var writer = db.CreateContext())
        {
            var other = await writer.Devices.SingleAsync(d => d.Id == deviceId);
            other.MaintenanceNotes = "concurrent write";
            await writer.SaveChangesAsync();
        }

        // Repository UpdateAsync surfaces ConcurrencyConflictException
        // (translated from DbUpdateConcurrencyException).
        var repository = new DeviceRepository(handlerContext);
        loaded.Name = "PC-RACE-EDITED";

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => repository.UpdateAsync(loaded));
        Assert.IsType<DbUpdateConcurrencyException>(ex.InnerException);

        await using var verify = db.CreateContext();
        var final = await verify.Devices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        Assert.Equal("PC-RACE", final.Name);
        Assert.Equal("concurrent write", final.MaintenanceNotes);
    }

    [Fact]
    public async Task DisposableSchema_IsIsolated_FromPublicSchema_AndPreservesPc01()
    {
        await using var db = await CreateOrFailAsync();

        await using (var context = db.CreateContext())
        {
            context.Devices.Add(NewPc("PC-ISOLATED"));
            await context.SaveChangesAsync();
        }

        // The device exists in the disposable schema.
        await using var scoped = db.CreateContext();
        Assert.Equal(1, await scoped.Devices.CountAsync(d => d.Name == "PC-ISOLATED"));

        // Public schema is untouched: no test device, PC-01 still present.
        var publicConn = new NpgsqlConnectionStringBuilder(db.ConnectionString)
        {
            SearchPath = "public"
        }.ConnectionString;
        await using var publicContext = new CashierDbContext(
            new DbContextOptionsBuilder<CashierDbContext>().UseNpgsql(publicConn).Options);
        Assert.Equal(0, await publicContext.Devices.CountAsync(d => d.Name == "PC-ISOLATED"));
        Assert.Equal(1, await publicContext.Devices.CountAsync(d => d.Name == "PC-01" && !d.IsDeleted));
    }
}
