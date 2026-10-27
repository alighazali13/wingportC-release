using System.Net;
using GamePort.Cashier.Application;
using GamePort.Cashier.Infrastructure;
using GamePort.Cashier.Infrastructure.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// Guards the real application pipeline wiring (App.xaml.cs uses MapCashierServer).
/// Devices cannot connect unless the SignalR hub and /health are mapped, so this
/// asserts the endpoint surface the WPF host actually exposes.
/// </summary>
public class ServerPipelineTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Urls:0"] = "http://127.0.0.1:0",
                ["Server:ApiKey"] = "pipeline-test-key",
                ["Database:AutoMigrate"] = "false",
                ["Backup:Enabled"] = "false",
                ["Cloud:Enabled"] = "false",
                ["Storage:DataDirectory"] = Path.Combine(Path.GetTempPath(), "cashier-pipeline-test")
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        builder.WebHost.UseTestServer();
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(null);
        builder.Services.AddCashierServer(builder.Configuration);

        // Background workers must not run during the test.
        foreach (var descriptor in builder.Services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
        {
            builder.Services.Remove(descriptor);
        }

        var app = builder.Build();
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCashierServer();

        await app.StartAsync();
        _app = app;
        _client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task HealthEndpoint_IsReachable()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void SignalRClientHubAndApiEndpoints_AreMapped()
    {
        var routes = RoutePatterns()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.Contains("/hubs/client", routes);
        Assert.Contains(routes, r => r.StartsWith("/api/", StringComparison.Ordinal));
    }

    [Fact]
    public void HealthEndpoint_DoesNotRequireAuthorization()
    {
        var health = RoutePatterns()
            .Single(e => (e.RoutePattern.RawText ?? string.Empty) == "/health");

        Assert.Null(health.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>());
    }

    private IEnumerable<RouteEndpoint> RoutePatterns() =>
        ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>();
}
