using System.Text.Json;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// Isolated PostgreSQL test database for C2-B concurrency tests.
/// Creates a disposable schema per test and scopes all EF table access to it via
/// the Npgsql "Search Path" connection-string option, so the real public schema
/// (and PC-01) is never touched. Requires a reachable local PostgreSQL.
/// Connection string resolution order:
///   1. WINGPORT_TEST_DB environment variable (full Npgsql connection string)
///   2. ConnectionStrings:DefaultConnection from %LocalAppData%/GamePort/Cashier/appsettings.json
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _baseConnectionString;
    private readonly string _schemaName;

    public string ConnectionString { get; }

    public bool IsAvailable { get; }

    private PostgresTestDatabase(string baseConnectionString, string schemaName, bool isAvailable)
    {
        _baseConnectionString = baseConnectionString;
        _schemaName = schemaName;
        ConnectionString = BuildScopedConnectionString(baseConnectionString, schemaName);
        IsAvailable = isAvailable;
    }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var baseConn = ResolveBaseConnectionString();
        if (baseConn is null)
        {
            return new PostgresTestDatabase(string.Empty, string.Empty, isAvailable: false);
        }

        // Fail fast (and mark unavailable) when PostgreSQL is unreachable.
        try
        {
            await using var probe = new NpgsqlConnection(baseConn);
            await probe.OpenAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            return new PostgresTestDatabase(baseConn, string.Empty, isAvailable: false);
        }

        var schema = "c2b_t_" + Guid.NewGuid().ToString("N")[..16];
        await using (var admin = new NpgsqlConnection(baseConn))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA \"{schema}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        return new PostgresTestDatabase(baseConn, schema, isAvailable: true);
    }

    public CashierDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CashierDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new CashierDbContext(options);
    }

    /// <summary>
    /// Applies the full EF migration chain inside the disposable schema.
    /// EnsureCreated cannot be used here: it checks for table existence across the
    /// database and would skip creation because the public schema already has tables.
    /// Migrations run with the connection's SearchPath, so all tables (including
    /// __EFMigrationsHistory) are created inside the disposable schema only.
    /// </summary>
    public async Task EnsureSchemaCreatedAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DropAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            await using var admin = new NpgsqlConnection(_baseConnectionString);
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{_schemaName}\" CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup — disposable schemas expire with the database.
        }
    }

    public ValueTask DisposeAsync() => new(DropAsync());

    private static string BuildScopedConnectionString(string baseConnectionString, string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema
        };
        return builder.ConnectionString;
    }

    private static string? ResolveBaseConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("WINGPORT_TEST_DB");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var appsettings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GamePort", "Cashier", "appsettings.json");

        if (!File.Exists(appsettings))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(appsettings));
            var conn = doc.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString();
            return string.IsNullOrWhiteSpace(conn) ? null : conn;
        }
        catch
        {
            return null;
        }
    }
}
