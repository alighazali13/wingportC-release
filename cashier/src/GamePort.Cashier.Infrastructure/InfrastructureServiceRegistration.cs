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
        services.AddDbContext<CashierDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("WingPortDev");
            }
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
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
