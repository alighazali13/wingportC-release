using System.Reflection;
using GamePort.Cashier.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class MigrationChainTests
{
    private static readonly string[] ExpectedMigrationIds =
    [
        "20260916110252_InitialCreate",
        "20260916111716_AddSessionPauseTracking",
        "20260916112825_AddWalletBilling",
        "20260916144126_AddReservations",
        "20260916163003_AddGames",
        "20260916170826_AddEmployeesAndAttendance",
        "20260916171320_AddOutboxNextAttempt"
    ];

    [Fact]
    public void MigrationChain_HasExpectedCount()
    {
        var migrations = GetMigrationTypes();
        Assert.Equal(ExpectedMigrationIds.Length, migrations.Count);
    }

    [Fact]
    public void MigrationChain_ContainsAllExpectedMigrations()
    {
        var migrations = GetMigrationTypes();
        var migrationIds = migrations.Select(GetMigrationId).ToList();

        foreach (var expected in ExpectedMigrationIds)
        {
            Assert.Contains(expected, migrationIds);
        }
    }

    [Fact]
    public void MigrationChain_IsInChronologicalOrder()
    {
        var migrations = GetMigrationTypes();
        var migrationIds = migrations.Select(GetMigrationId).ToList();

        for (var i = 0; i < ExpectedMigrationIds.Length; i++)
        {
            Assert.Equal(ExpectedMigrationIds[i], migrationIds[i]);
        }
    }

    [Fact]
    public void AddReservations_UsesPostgresSafeSqlForTypeConversion()
    {
        var sourceDir = FindSourceDir();
        Assert.NotNull(sourceDir);

        var migrationFile = Path.Combine(sourceDir!, "Migrations", "20260916144126_AddReservations.cs");
        Assert.True(File.Exists(migrationFile), $"Migration file not found: {migrationFile}");

        var migrationSource = File.ReadAllText(migrationFile);
        Assert.Contains("USING", migrationSource);
        Assert.Contains("migrationBuilder.Sql", migrationSource);
    }

    [Fact]
    public void MigrationFiles_AllExistOnDisk()
    {
        var sourceDir = FindSourceDir();
        Assert.NotNull(sourceDir);

        var migrationsDir = Path.Combine(sourceDir!, "Migrations");

        foreach (var id in ExpectedMigrationIds)
        {
            var migrationFile = Path.Combine(migrationsDir, $"{id}.cs");
            Assert.True(File.Exists(migrationFile), $"Migration file not found: {id}.cs");
        }

        var snapshotFile = Path.Combine(migrationsDir, "CashierDbContextModelSnapshot.cs");
        Assert.True(File.Exists(snapshotFile), "CashierDbContextModelSnapshot.cs not found");
    }

    private static string? FindSourceDir()
    {
        var assembly = typeof(AddReservations).Assembly;
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

    private static string GetMigrationId(Type migrationType)
    {
        var attr = migrationType.GetCustomAttribute<MigrationAttribute>();
        return attr?.Id ?? throw new InvalidOperationException($"Migration {migrationType.Name} has no [Migration] attribute");
    }

    private static List<Type> GetMigrationTypes()
    {
        var assembly = typeof(AddReservations).Assembly;
        return assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => GetMigrationId(t))
            .ToList();
    }
}
