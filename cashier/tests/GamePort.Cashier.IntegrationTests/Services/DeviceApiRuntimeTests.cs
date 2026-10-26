using System.Net;
using System.Net.Http.Json;
using GamePort.Cashier.Application;
using GamePort.Cashier.Contracts.Common;
using GamePort.Cashier.Contracts.Responses;
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
/// C2-B HTTP runtime tests for the device API (register, GET, PUT with xmin)
/// running the real endpoint pipeline against an isolated disposable PostgreSQL schema.
/// Authentication uses the API-key scheme (grants devices.manage).
/// </summary>
public class DeviceApiRuntimeTests : IAsyncLifetime
{
    private const string ApiKey = "c2b-runtime-test-key";

    private readonly ITestOutputHelper _output;
    private PostgresTestDatabase _db = null!;
    private IHost _host = null!;
    private HttpClient _client = null!;

    public DeviceApiRuntimeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _db = await PostgresTestDatabase.CreateAsync();
        if (!_db.IsAvailable)
        {
            Assert.Fail("PostgreSQL required for API runtime verification. Set WINGPORT_TEST_DB or make the local development database reachable.");
        }

        await _db.EnsureSchemaCreatedAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _db.ConnectionString,
                ["Server:ApiKey"] = ApiKey,
                ["Server:Urls:0"] = "http://127.0.0.1:0",
                ["Database:UseInMemory"] = "false",
                ["Database:AutoMigrate"] = "false",
                ["Backup:Enabled"] = "false",
                ["Cloud:Enabled"] = "false",
                ["Storage:DataDirectory"] = Path.Combine(Path.GetTempPath(), "c2b-api-test")
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        builder.WebHost.UseTestServer();
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(_db.ConnectionString);
        builder.Services.AddCashierServer(builder.Configuration);

        // Remove all hosted services — background workers must not interfere with API tests.
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

    [Fact]
    public async Task Post_RegisterGamingPC_ReturnsSecretOnce_AndV2Fields()
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name = "PC-API-01",
            type = "GamingPC",
            ipAddress = "10.10.0.1",
            displayOrder = 2
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await UnwrapAsync<RegisteredDeviceResponse>(response);
        Assert.NotNull(data);
        Assert.Equal("GamingPC", data!.Type);
        Assert.Equal("Client", data.ConnectionMode);
        Assert.Equal("Disconnected", data.ConnectivityStatus);
        Assert.Equal("Available", data.OperationalStatus);
        Assert.True(data.IsActive);
        Assert.False(string.IsNullOrEmpty(data.ClientSecret));
        Assert.False(string.IsNullOrEmpty(data.ClientIdentity));
        Assert.False(string.IsNullOrEmpty(data.Xmin));
        Assert.True(uint.TryParse(data.Xmin, out var xmin) && xmin > 0);

        // GET must never expose the secret.
        var getResponse = await _client.GetAsync($"/api/devices/{data.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.ClientSecret, body);
        Assert.DoesNotContain("clientSecretHash", body, StringComparison.OrdinalIgnoreCase);

        var fetched = await UnwrapAsync<DeviceResponse>(getResponse);
        Assert.Equal(data.Id, fetched!.Id);
        Assert.Equal("PC-API-01", fetched.Name);
        Assert.False(string.IsNullOrEmpty(fetched.Xmin));
    }

    [Fact]
    public async Task Post_RegisterPlayStation_WithoutIpOrCredentials_Succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name = "PS5-API-01",
            type = "PlayStation",
            consoleModel = "Slim",
            controllerCount = 2,
            maxPlayers = 4
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await UnwrapAsync<RegisteredDeviceResponse>(response);
        Assert.NotNull(data);
        Assert.Equal("Manual", data!.ConnectionMode);
        Assert.Equal("NotApplicable", data.ConnectivityStatus);
        Assert.Equal(string.Empty, data.ClientSecret);
        Assert.Null(data.ClientIdentity);
        Assert.Equal(string.Empty, data.IpAddress);
        Assert.Equal("Slim", data.ConsoleModel);
        Assert.Equal(2, data.ControllerCount);
        Assert.Equal(4, data.MaxPlayers);
    }

    [Fact]
    public async Task Post_InvalidZoneId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name = "PC-API-Z",
            type = "GamingPC",
            ipAddress = "10.10.0.2",
            zoneId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidPricingGroup_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name = "PC-API-P",
            type = "GamingPC",
            ipAddress = "10.10.0.3",
            pricingGroupId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_PricingGroupDeviceTypeMismatch_Returns400()
    {
        Guid pcGroupId;
        await using (var seed = _db.CreateContext())
        {
            var group = new GamePort.Cashier.Domain.Entities.PricingGroup
            {
                Name = "PC Only API",
                DeviceType = GamePort.Cashier.Domain.Enums.DeviceType.GamingPC,
                IsActive = true
            };
            seed.PricingGroups.Add(group);
            await seed.SaveChangesAsync();
            pcGroupId = group.Id;
        }

        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name = "PS5-API-M",
            type = "PlayStation",
            pricingGroupId = pcGroupId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithFreshXmin_Succeeds_AndReturnsNewXmin()
    {
        var registered = await RegisterPcAsync("PC-API-PUT");

        var updateResponse = await _client.PutAsJsonAsync($"/api/devices/{registered.Id}", new
        {
            name = "PC-API-PUT-2",
            ipAddress = "10.10.0.50",
            displayOrder = 9,
            maintenanceNotes = "updated via API",
            xmin = registered.Xmin
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await UnwrapAsync<DeviceResponse>(updateResponse);
        Assert.NotNull(updated);
        Assert.Equal("PC-API-PUT-2", updated!.Name);
        Assert.Equal("10.10.0.50", updated.IpAddress);
        Assert.Equal(9, updated.DisplayOrder);
        Assert.Equal("updated via API", updated.MaintenanceNotes);
        Assert.NotEqual(registered.Xmin, updated.Xmin);

        // Immutable fields untouched.
        Assert.Equal("GamingPC", updated.Type);
        Assert.Equal(registered.ClientIdentity, updated.ClientIdentity);
        Assert.Equal("Client", updated.ConnectionMode);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Put_WithStaleXmin_Returns409()
    {
        var registered = await RegisterPcAsync("PC-API-STALE");

        // First update succeeds and bumps xmin.
        var first = await _client.PutAsJsonAsync($"/api/devices/{registered.Id}", new
        {
            name = "PC-API-STALE-1",
            ipAddress = "10.10.0.60",
            xmin = registered.Xmin
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replaying the old token must conflict.
        var second = await _client.PutAsJsonAsync($"/api/devices/{registered.Id}", new
        {
            name = "PC-API-STALE-2",
            ipAddress = "10.10.0.61",
            xmin = registered.Xmin
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Put_MissingXmin_Returns400()
    {
        var registered = await RegisterPcAsync("PC-API-NOXMIN");

        var response = await _client.PutAsJsonAsync($"/api/devices/{registered.Id}", new
        {
            name = "PC-API-NOXMIN-2",
            ipAddress = "10.10.0.70",
            xmin = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_InvalidXminToken_Returns400()
    {
        var registered = await RegisterPcAsync("PC-API-BADXMIN");

        var response = await _client.PutAsJsonAsync($"/api/devices/{registered.Id}", new
        {
            name = "PC-API-BADXMIN-2",
            ipAddress = "10.10.0.71",
            xmin = "not-a-number"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_UnknownDevice_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/devices/{Guid.NewGuid()}", new
        {
            name = "X",
            ipAddress = "10.10.0.80",
            xmin = "1"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ListAndById_ReturnV2Fields_AndZoneName()
    {
        Guid zoneId;
        await using (var seed = _db.CreateContext())
        {
            var zone = new GamePort.Cashier.Domain.Entities.Zone
            {
                Name = "API Zone",
                IsActive = true,
                DisplayOrder = 1
            };
            seed.Zones.Add(zone);
            await seed.SaveChangesAsync();
            zoneId = zone.Id;
        }

        var registered = await RegisterPcAsync("PC-API-ZONE", zoneId);

        var getResponse = await _client.GetAsync($"/api/devices/{registered.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var device = await UnwrapAsync<DeviceResponse>(getResponse);
        Assert.Equal(zoneId, device!.ZoneId);
        Assert.Equal("API Zone", device.ZoneName);

        var listResponse = await _client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await UnwrapAsync<List<DeviceResponse>>(listResponse);
        Assert.NotNull(list);
        Assert.Contains(list!, d => d.Id == registered.Id && d.ZoneName == "API Zone");
    }

    [Fact]
    public async Task Put_InvalidZoneReference_Returns400()
    {
        var registered = await RegisterPcAsync("PC-API-PUTZ");

        var response = await _client.PutAsJsonAsync($"/api/devices/{registered.Id}", new
        {
            name = registered.Name,
            ipAddress = registered.IpAddress,
            zoneId = Guid.NewGuid(),
            xmin = registered.Xmin
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_DoesNotModifyActiveSessionOrReservation()
    {
        Guid deviceId;
        uint xmin;
        await using (var seed = _db.CreateContext())
        {
            var device = new GamePort.Cashier.Domain.Entities.Device
            {
                Id = Guid.NewGuid(),
                Name = "PC-API-SESS",
                Type = GamePort.Cashier.Domain.Enums.DeviceType.GamingPC,
                Status = GamePort.Cashier.Domain.Enums.DeviceStatus.Available,
                IpAddress = "10.10.0.90",
                ConnectionMode = GamePort.Cashier.Domain.Enums.ConnectionMode.Client,
                ConnectivityStatus = GamePort.Cashier.Domain.Enums.ConnectivityStatus.Disconnected,
                OperationalStatus = GamePort.Cashier.Domain.Enums.OperationalStatus.Available,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var customer = new GamePort.Cashier.Domain.Entities.Customer
            {
                Name = "API Customer",
                PhoneNumber = "09120000099",
                CreatedAt = DateTime.UtcNow
            };
            seed.Devices.Add(device);
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();

            seed.Sessions.Add(new GamePort.Cashier.Domain.Entities.Session
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                DeviceId = device.Id,
                DeviceType = GamePort.Cashier.Domain.Enums.DeviceType.GamingPC,
                StartTime = DateTime.UtcNow,
                PriceAtStart = 5000m,
                Status = GamePort.Cashier.Domain.Enums.SessionStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();

            deviceId = device.Id;
            xmin = device.xmin;
        }

        var response = await _client.PutAsJsonAsync($"/api/devices/{deviceId}", new
        {
            name = "PC-API-SESS-2",
            ipAddress = "10.10.0.91",
            xmin = xmin.ToString()
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verify = _db.CreateContext();
        var session = await verify.Sessions.SingleAsync(s => s.DeviceId == deviceId);
        Assert.Equal(GamePort.Cashier.Domain.Enums.SessionStatus.Active, session.Status);
        Assert.Equal("PC-API-SESS-2", (await verify.Devices.AsNoTracking().SingleAsync(d => d.Id == deviceId)).Name);
    }

    private async Task<RegisteredDeviceResponse> RegisterPcAsync(string name, Guid? zoneId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new
        {
            name,
            type = "GamingPC",
            ipAddress = "10.10.0.9",
            zoneId
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await UnwrapAsync<RegisteredDeviceResponse>(response);
        Assert.NotNull(data);
        return data!;
    }
}
