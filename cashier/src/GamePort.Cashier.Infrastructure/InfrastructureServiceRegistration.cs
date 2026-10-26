using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string? connectionString)
    {
#if !DEBUG
        // Release builds must NOT silently fall back to InMemory.
        // InMemory loses all data on restart — dangerous for a production Cashier.
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Database configuration is missing. " +
                "GamePort Cashier requires a persistent PostgreSQL connection. " +
                "Please configure the database connection in appsettings.json " +
                "under %LocalAppData%\\GamePort\\Cashier\\.");
        }
#endif

        services.AddDbContext<CashierDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                // Development fallback only — guarded above for Release builds
                options.UseInMemoryDatabase("WingPortDev");
            }
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IPricingGroupRepository, PricingGroupRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IDeviceGameRepository, DeviceGameRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<ISyncCheckpointRepository, SyncCheckpointRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();

        services.AddSingleton<ISecretHasher, Pbkdf2SecretHasher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
