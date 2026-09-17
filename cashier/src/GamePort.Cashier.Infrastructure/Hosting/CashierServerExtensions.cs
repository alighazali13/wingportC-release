using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Infrastructure.Authentication;
using GamePort.Cashier.Infrastructure.Backup;
using GamePort.Cashier.Infrastructure.Communication.SignalR;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Sync;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace GamePort.Cashier.Infrastructure.Hosting;

public static class CashierServerExtensions
{
    public static IServiceCollection AddCashierServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CashierServerOptions>(configuration.GetSection(CashierServerOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CloudOptions>(configuration.GetSection(CloudOptions.SectionName));
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));

        services.AddSignalR();
        services.AddHealthChecks().AddDbContextCheck<CashierDbContext>("database");

        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

        AddRateLimiting(services);
        AddAuthenticationAndAuthorization(services, LocalPaths.Resolve(configuration));

        services.AddHttpClient<ICloudSyncClient, HttpCloudSyncClient>();

        services.AddScoped<IBackupService, PostgresBackupService>();

        services.AddSingleton<IDeviceConnectionManager, DeviceConnectionManager>();
        services.AddSingleton<IDeviceCommandSender, SignalRDeviceCommandSender>();
        services.AddScoped<IDeviceAuthenticator, DeviceAuthenticator>();

        services.AddHostedService<DeviceHeartbeatMonitor>();
        services.AddHostedService<SessionExpirationService>();
        services.AddHostedService<SessionRecoveryService>();
        services.AddHostedService<SessionBillingWorker>();
        services.AddHostedService<ReservationExpirationService>();
        services.AddHostedService<EmployeeBootstrapService>();
        services.AddHostedService<CloudSyncWorker>();
        services.AddHostedService<BackupWorker>();

        return services;
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.PermitLimit = 10;
                limiter.QueueLimit = 0;
            });
        });
    }

    private static void AddAuthenticationAndAuthorization(IServiceCollection services, string dataDirectory)
    {
        var signingKey = new SymmetricSecurityKey(JwtSigningKeyProvider.GetOrCreateKey(dataDirectory));
        services.AddSingleton(new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        services.AddScoped<ITokenService, JwtTokenService>();

        var jwtOptions = new JwtOptions();
        services.AddSingleton(jwtOptions);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.Scheme, _ => { });

        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy =>
                {
                    policy.AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        ApiKeyAuthenticationDefaults.Scheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(JwtTokenService.PermissionClaim, permission);
                });
            }
        });
    }

    public static void MapCashierServer(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health");
        endpoints.MapHub<ClientHub>("/hubs/client");
        endpoints.MapCashierApi();
    }

    public static WebApplication UseCashierServer(this WebApplication app)
    {
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static string[] GetServerUrls(this IConfiguration configuration)
    {
        return configuration.GetSection(CashierServerOptions.SectionName).Get<CashierServerOptions>()?.Urls
               ?? new[] { "http://0.0.0.0:5210" };
    }
}
