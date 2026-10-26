using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

/// <summary>
/// Regression tests for Device V2 enum values, entity defaults, and backfill mapping rules.
/// </summary>
public class DeviceV2MappingTests
{
    // ===== Enum value verification =====

    [Fact]
    public void OperationalStatus_HasCorrectValues()
    {
        Assert.Equal(1, (int)OperationalStatus.Available);
        Assert.Equal(2, (int)OperationalStatus.InUse);
        Assert.Equal(3, (int)OperationalStatus.Maintenance);
        Assert.Equal(3, Enum.GetValues<OperationalStatus>().Length);
    }

    [Fact]
    public void ConnectivityStatus_HasCorrectValues()
    {
        Assert.Equal(1, (int)ConnectivityStatus.Connected);
        Assert.Equal(2, (int)ConnectivityStatus.Disconnected);
        Assert.Equal(3, (int)ConnectivityStatus.NotApplicable);
        Assert.Equal(3, Enum.GetValues<ConnectivityStatus>().Length);
    }

    [Fact]
    public void ConnectionMode_HasCorrectValues()
    {
        Assert.Equal(1, (int)ConnectionMode.Client);
        Assert.Equal(2, (int)ConnectionMode.Manual);
        Assert.Equal(2, Enum.GetValues<ConnectionMode>().Length);
    }

    [Fact]
    public void DeviceStatus_LegacyValuesUnchanged()
    {
        Assert.Equal(1, (int)DeviceStatus.Available);
        Assert.Equal(2, (int)DeviceStatus.InUse);
        Assert.Equal(3, (int)DeviceStatus.Offline);
        Assert.Equal(4, (int)DeviceStatus.Disconnected);
        Assert.Equal(5, (int)DeviceStatus.Maintenance);
        Assert.Equal(5, Enum.GetValues<DeviceStatus>().Length);
    }

    // ===== Entity defaults =====

    [Fact]
    public void Device_NewDevice_HasV2Defaults()
    {
        var device = new Device();
        Assert.True(device.IsActive);
        Assert.Equal(OperationalStatus.Available, device.OperationalStatus);
        Assert.Equal(ConnectivityStatus.NotApplicable, device.ConnectivityStatus);
        Assert.Equal(ConnectionMode.Client, device.ConnectionMode);
        Assert.Equal(0, device.DisplayOrder);
        Assert.False(device.IsConnected);
        Assert.Null(device.ZoneId);
        Assert.Null(device.PricingGroupId);
        Assert.Null(device.IpAddress);
        Assert.Null(device.ConsoleModel);
        Assert.Null(device.ControllerCount);
        Assert.Null(device.MaxPlayers);
        Assert.Null(device.MaintenanceNotes);
        Assert.Null(device.LastMaintenanceAt);
    }

    [Fact]
    public void Zone_NewZone_HasDefaults()
    {
        var zone = new Zone();
        Assert.True(zone.IsActive);
        Assert.Equal(0, zone.DisplayOrder);
        Assert.Null(zone.ParentId);
    }

    [Fact]
    public void PricingGroup_NewGroup_HasDefaults()
    {
        var group = new PricingGroup();
        Assert.True(group.IsActive);
        Assert.Null(group.Description);
    }

    [Fact]
    public void PricingRule_NewRule_HasNullPricingGroup()
    {
        var rule = new PricingRule();
        Assert.Null(rule.PricingGroupId);
        Assert.True(rule.IsActive);
    }

    // ===== Backfill mapping (pure logic, mirrors migration SQL) =====

    /// <summary>
    /// Mirrors the CASE statement in AddDeviceV2 migration for OperationalStatus.
    /// </summary>
    private static OperationalStatus MapLegacyStatusToDeviceOperationalStatus(DeviceStatus legacy)
    {
        return legacy switch
        {
            DeviceStatus.Available => OperationalStatus.Available,
            DeviceStatus.InUse => OperationalStatus.InUse,
            DeviceStatus.Offline => OperationalStatus.Available,
            DeviceStatus.Disconnected => OperationalStatus.Available,
            DeviceStatus.Maintenance => OperationalStatus.Maintenance,
            _ => OperationalStatus.Available
        };
    }

    /// <summary>
    /// Mirrors the CASE statement in AddDeviceV2 migration for ConnectivityStatus.
    /// </summary>
    private static ConnectivityStatus MapDeviceConnectivity(DeviceType type, bool isConnected)
    {
        if (type == DeviceType.PlayStation) return ConnectivityStatus.NotApplicable;
        return isConnected ? ConnectivityStatus.Connected : ConnectivityStatus.Disconnected;
    }

    /// <summary>
    /// Mirrors the CASE statement in AddDeviceV2 migration for ConnectionMode.
    /// </summary>
    private static ConnectionMode MapDeviceConnectionMode(DeviceType type)
    {
        return type == DeviceType.PlayStation ? ConnectionMode.Manual : ConnectionMode.Client;
    }

    [Theory]
    [InlineData(DeviceStatus.Available, OperationalStatus.Available)]
    [InlineData(DeviceStatus.InUse, OperationalStatus.InUse)]
    [InlineData(DeviceStatus.Offline, OperationalStatus.Available)]
    [InlineData(DeviceStatus.Disconnected, OperationalStatus.Available)]
    [InlineData(DeviceStatus.Maintenance, OperationalStatus.Maintenance)]
    public void Backfill_MapsLegacyStatusToOperationalStatus(DeviceStatus legacy, OperationalStatus expected)
    {
        Assert.Equal(expected, MapLegacyStatusToDeviceOperationalStatus(legacy));
    }

    [Fact]
    public void Backfill_MaintenanceMapsToOperationalMaintenance()
    {
        Assert.Equal(OperationalStatus.Maintenance, MapLegacyStatusToDeviceOperationalStatus(DeviceStatus.Maintenance));
        Assert.Equal(3, (int)MapLegacyStatusToDeviceOperationalStatus(DeviceStatus.Maintenance));
    }

    [Fact]
    public void Backfill_PCConnectedMapsToConnected()
    {
        Assert.Equal(ConnectivityStatus.Connected, MapDeviceConnectivity(DeviceType.GamingPC, true));
    }

    [Fact]
    public void Backfill_PCDisconnectedMapsToDisconnected()
    {
        Assert.Equal(ConnectivityStatus.Disconnected, MapDeviceConnectivity(DeviceType.GamingPC, false));
    }

    [Fact]
    public void Backfill_PS5MapsToNotApplicable()
    {
        Assert.Equal(ConnectivityStatus.NotApplicable, MapDeviceConnectivity(DeviceType.PlayStation, false));
        Assert.Equal(ConnectivityStatus.NotApplicable, MapDeviceConnectivity(DeviceType.PlayStation, true));
    }

    [Fact]
    public void Backfill_PCMapsToClientMode()
    {
        Assert.Equal(ConnectionMode.Client, MapDeviceConnectionMode(DeviceType.GamingPC));
    }

    [Fact]
    public void Backfill_PS5MapsToManualMode()
    {
        Assert.Equal(ConnectionMode.Manual, MapDeviceConnectionMode(DeviceType.PlayStation));
    }

    // ===== Migration file content verification =====

    [Fact]
    public void AddDeviceV2_MigrationFile_ContainsBackfillSql()
    {
        var sourceDir = FindSourceDir();
        Assert.NotNull(sourceDir);
        var migrationFile = Path.Combine(sourceDir!, "Migrations", "20261010191117_AddDeviceV2.cs");
        Assert.True(File.Exists(migrationFile), $"Migration file not found: {migrationFile}");

        var source = File.ReadAllText(migrationFile);

        // Verify backfill SQL is present (C# escaped quotes in migration source)
        Assert.Contains("OperationalStatus", source);
        Assert.Contains("ConnectivityStatus", source);
        Assert.Contains("ConnectionMode", source);
        Assert.Contains("WHEN 5 THEN 3", source); // Maintenance=5 → Operational Maintenance=3
        Assert.Contains("WHEN \"\"Type\"\" = 2 THEN 3", source); // PS5 → NotApplicable (C# escaped)
        Assert.Contains("WHEN \"\"Type\"\" = 2 THEN 2", source); // PS5 → Manual (C# escaped)
        Assert.Contains("NOT \"\"IsDeleted\"\"", source); // IsActive from IsDeleted (C# escaped)
    }

    [Fact]
    public void AddDeviceV2_MigrationFile_HasRestrictForeignKeys()
    {
        var sourceDir = FindSourceDir();
        Assert.NotNull(sourceDir);
        var migrationFile = Path.Combine(sourceDir!, "Migrations", "20261010191117_AddDeviceV2.cs");
        var source = File.ReadAllText(migrationFile);
        Assert.Contains("ReferentialAction.Restrict", source);
    }

    [Fact]
    public void AddDeviceXminMigration_Exists()
    {
        var sourceDir = FindSourceDir();
        Assert.NotNull(sourceDir);
        var migrationFile = Path.Combine(sourceDir!, "Migrations", "20261010231654_AddDeviceXminConcurrency.cs");
        Assert.True(File.Exists(migrationFile), $"xmin migration not found: {migrationFile}");

        var source = File.ReadAllText(migrationFile);
        // xmin migration should reference xid type and rowVersion
        Assert.Contains("xid", source);
        Assert.Contains("rowVersion: true", source);
    }

    private static string? FindSourceDir()
    {
        var assembly = typeof(DeviceV2MappingTests).Assembly;
        var location = assembly.Location;
        var dir = Path.GetDirectoryName(location)!;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "cashier", "src", "GamePort.Cashier.Infrastructure", "Data");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
