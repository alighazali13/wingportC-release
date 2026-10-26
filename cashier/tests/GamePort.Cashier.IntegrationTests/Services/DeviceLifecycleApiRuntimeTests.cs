using System.Net;
using System.Net.Http.Json;
using GamePort.Cashier.Application;
using GamePort.Cashier.Contracts.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure;
using GamePort.Cashier.Infrastructure.Authentication;
using GamePort.Cashier.Infrastructure.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// C2-C HTTP runtime tests for PATCH activation, PATCH operational-status and
/// DELETE with If-Match, running the real endpoint pipeline against an isolated
/// disposable PostgreSQL schema.
/// </summary>
public class DeviceLifecycleApiRuntimeTests : IAsyncLifetime
{
    private const string ApiKey = "c2c-lifecycle-test-key";

    private readonly ITestOutputHelper _output;
    private PostgresTestDatabase _db = null!;
    private IHost _host = null!;
    private HttpClient _client = null!;

    public DeviceLifecycleApiRuntimeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _db = await PostgresTestDatabase.CreateAsync();
        if (!_db.IsAvailable)
        {
            Assert.Fail("PostgreSQL required for lifecycle API runtime verification. Set WINGPORT_TEST_DB or make the local development database reachable.");
        }

        await _db.EnsureSchemaCreatedAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _db.ConnectionString,
                ["Server:ApiKey"] = ApiKey,
                ["Database:UseInMemory"] = "false",
                ["Database:AutoMigrate"] = "false",
                ["Backup:Enabled"] = "false",
                ["Cloud:Enabled"] = "false",
                ["Storage:DataDirectory"] = Path.Combine(Path.GetTempPath(), "c2c-lifecycle-test")
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        builder.WebHost.UseTestServer();
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(_db.ConnectionString);
        builder.Services.AddCashierServer(builder.Configuration);

        var hosted = builder.Services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        foreach (var descriptor in hosted)
        {
            builder.Services.Remove(descriptor);
        }

        var app = builder.Build();
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCashierApi();

        await app.StartAsync();
        _host = app;
        _client = app.GetTestClient();
        _client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, ApiKey);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            await ((IAsyncDisposable)_host).DisposeAsync();
        }

        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }

    private static async Task<T?> UnwrapAsync<T>(HttpResponseMessage response) where T : class
    {
        var json = await response.Content.ReadFromJsonAsync<BaseResponse<T>>();
        return json?.Data;
    }

    private async Task<DeviceResponse> RegisterPcAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name,
            type = "GamingPC",
            ipAddress = "10.20.0.1"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await UnwrapAsync<RegisteredDeviceResponse>(response);
        Assert.NotNull(data);
        return data!;
    }

    // ===================== PATCH activation =====================

    [Fact]
    public async Task PatchActivation_DeactivateAndReactivate_Succeeds()
    {
        var device = await RegisterPcAsync("PC-ACT-API");

        var deactivate = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await UnwrapAsync<DeviceResponse>(deactivate);
        Assert.False(deactivated!.IsActive);
        Assert.NotEqual(device.Xmin, deactivated.Xmin);

        var reactivate = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = true, xmin = deactivated.Xmin });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var reactivated = await UnwrapAsync<DeviceResponse>(reactivate);
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task PatchActivation_StaleXmin_Returns409()
    {
        var device = await RegisterPcAsync("PC-ACT-STALE");

        var first = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = true, xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task PatchActivation_MissingOrInvalidXmin_Returns400()
    {
        var device = await RegisterPcAsync("PC-ACT-BAD");

        var missing = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var invalid = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = "not-a-number" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task PatchActivation_UnknownDevice_Returns404()
    {
        var response = await _client.PatchAsJsonAsync($"/api/devices/{Guid.NewGuid()}/activation",
            new { isActive = false, xmin = "1" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchActivation_ReactivationAfterMaintenance_PreservesMaintenance()
    {
        var device = await RegisterPcAsync("PC-ACT-MAINT");

        var toMaintenance = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "Maintenance", maintenanceNotes = "repair", xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.OK, toMaintenance.StatusCode);
        var maintained = await UnwrapAsync<DeviceResponse>(toMaintenance);

        var deactivate = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = maintained!.Xmin });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await UnwrapAsync<DeviceResponse>(deactivate);

        var reactivate = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = true, xmin = deactivated!.Xmin });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var reactivated = await UnwrapAsync<DeviceResponse>(reactivate);

        Assert.True(reactivated!.IsActive);
        Assert.Equal("Maintenance", reactivated.OperationalStatus);
        Assert.Equal("Maintenance", reactivated.Status);
    }

    // ===================== PATCH operational-status =====================

    [Fact]
    public async Task PatchOperationalStatus_AvailableToMaintenance_AndBack()
    {
        var device = await RegisterPcAsync("PC-OPS-API");

        var toMaintenance = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "Maintenance", maintenanceNotes = "fan swap", xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.OK, toMaintenance.StatusCode);
        var maintained = await UnwrapAsync<DeviceResponse>(toMaintenance);
        Assert.Equal("Maintenance", maintained!.OperationalStatus);
        Assert.Equal("Maintenance", maintained.Status);
        Assert.Equal("fan swap", maintained.MaintenanceNotes);
        Assert.NotNull(maintained.LastMaintenanceAt);
        // Activation and connectivity unchanged.
        Assert.True(maintained.IsActive);
        Assert.Equal("Disconnected", maintained.ConnectivityStatus);

        var toAvailable = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "Available", xmin = maintained.Xmin });
        Assert.Equal(HttpStatusCode.OK, toAvailable.StatusCode);
        var available = await UnwrapAsync<DeviceResponse>(toAvailable);
        Assert.Equal("Available", available!.OperationalStatus);
    }

    [Fact]
    public async Task PatchOperationalStatus_ManualInUse_Returns400()
    {
        var device = await RegisterPcAsync("PC-OPS-INUSE");

        var response = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "InUse", xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchOperationalStatus_StaleXmin_Returns409()
    {
        var device = await RegisterPcAsync("PC-OPS-STALE");

        var first = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "Maintenance", xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "Available", xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task PatchOperationalStatus_ActiveSession_BlocksMaintenance_With409()
    {
        var device = await RegisterPcAsync("PC-OPS-SESS");

        // Seed an open session directly in the disposable schema.
        await using (var seed = _db.CreateContext())
        {
            var tracked = await seed.Devices.SingleAsync(d => d.Id == device.Id);
            var customer = new Customer { Name = "C", PhoneNumber = "09121111111", CreatedAt = DateTime.UtcNow };
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            seed.Sessions.Add(new Session
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
            // Keep the device operationally Available to isolate the session check.
            tracked.OperationalStatus = OperationalStatus.Available;
            tracked.Status = DeviceStatus.Available;
            await seed.SaveChangesAsync();
        }

        var current = await _client.GetAsync($"/api/devices/{device.Id}");
        var currentData = await UnwrapAsync<DeviceResponse>(current);

        var response = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/operational-status",
            new { operationalStatus = "Maintenance", xmin = currentData!.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ===================== DELETE with If-Match =====================

    [Fact]
    public async Task Delete_WithFreshIfMatch_Succeeds_SoftDelete()
    {
        var device = await RegisterPcAsync("PC-DEL-API");

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{device.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", device.Xmin);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get = await _client.GetAsync($"/api/devices/{device.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // Row preserved (soft delete).
        await using var verify = _db.CreateContext();
        var row = await verify.Devices.AsNoTracking().SingleAsync(d => d.Id == device.Id);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task Delete_QuotedEtagStyleIfMatch_Succeeds()
    {
        var device = await RegisterPcAsync("PC-DEL-ETAG");

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{device.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{device.Xmin}\"");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingIfMatch_Returns400()
    {
        var device = await RegisterPcAsync("PC-DEL-NOHDR");

        var response = await _client.DeleteAsync($"/api/devices/{device.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Device untouched.
        var get = await _client.GetAsync($"/api/devices/{device.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Delete_InvalidIfMatch_Returns400()
    {
        var device = await RegisterPcAsync("PC-DEL-BAD");

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{device.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "not-a-number");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_StaleIfMatch_Returns409()
    {
        var device = await RegisterPcAsync("PC-DEL-STALE");

        // Bump xmin.
        var bump = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = device.Xmin });
        Assert.Equal(HttpStatusCode.OK, bump.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{device.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", device.Xmin);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownDevice_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{Guid.NewGuid()}");
        request.Headers.TryAddWithoutValidation("If-Match", "1");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ActiveSession_BlocksWith409()
    {
        var device = await RegisterPcAsync("PC-DEL-SESS");

        await using (var seed = _db.CreateContext())
        {
            var customer = new Customer { Name = "C2", PhoneNumber = "09122222222", CreatedAt = DateTime.UtcNow };
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            seed.Sessions.Add(new Session
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
            await seed.SaveChangesAsync();
        }

        var current = await _client.GetAsync($"/api/devices/{device.Id}");
        var currentData = await UnwrapAsync<DeviceResponse>(current);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{device.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", currentData!.Xmin);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_FutureReservation_BlocksWith409()
    {
        var device = await RegisterPcAsync("PC-DEL-RES");

        await using (var seed = _db.CreateContext())
        {
            var customer = new Customer { Name = "C3", PhoneNumber = "09123333333", CreatedAt = DateTime.UtcNow };
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            seed.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                DeviceId = device.Id,
                DeviceType = DeviceType.GamingPC,
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
                Status = ReservationStatus.Reserved,
                CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var current = await _client.GetAsync($"/api/devices/{device.Id}");
        var currentData = await UnwrapAsync<DeviceResponse>(current);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/devices/{device.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", currentData!.Xmin);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_NoSecretsInErrorResponses()
    {
        var device = await RegisterPcAsync("PC-ERR-SECRET");

        var response = await _client.PatchAsJsonAsync($"/api/devices/{device.Id}/activation",
            new { isActive = false, xmin = device.Xmin });
        var body = await response.Content.ReadAsStringAsync();
        // ClientIdentity is a legitimate V1 response field; credentials must never appear.
        Assert.DoesNotContain("ClientSecretHash", body);
        Assert.DoesNotContain("clientSecretHash", body, StringComparison.OrdinalIgnoreCase);
    }
}
