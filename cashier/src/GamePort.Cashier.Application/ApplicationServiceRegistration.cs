using FluentValidation;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Services;
using GamePort.Cashier.Application.UseCases.Attendance;
using GamePort.Cashier.Application.UseCases.Auth;
using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Employees;
using GamePort.Cashier.Application.UseCases.Games;
using GamePort.Cashier.Application.UseCases.Reports;
using GamePort.Cashier.Application.UseCases.Reservations;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Application.UseCases.Sync;
using GamePort.Cashier.Application.UseCases.Wallets;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly);

        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<SessionBillingService>();

        services.AddScoped<RegisterDeviceHandler>();
        services.AddScoped<RegisterCustomerHandler>();

        services.AddScoped<StartSessionHandler>();
        services.AddScoped<EndSessionHandler>();
        services.AddScoped<ExtendSessionHandler>();
        services.AddScoped<PauseSessionHandler>();
        services.AddScoped<ResumeSessionHandler>();
        services.AddScoped<TransferSessionHandler>();
        services.AddScoped<CancelSessionHandler>();
        services.AddScoped<ExpireSessionsHandler>();
        services.AddScoped<ChargeActiveSessionsHandler>();

        services.AddScoped<RechargeWalletHandler>();

        services.AddScoped<CreateReservationHandler>();
        services.AddScoped<CancelReservationHandler>();
        services.AddScoped<ArriveReservationHandler>();
        services.AddScoped<ExpireReservationsHandler>();

        services.AddScoped<CreateGameHandler>();
        services.AddScoped<UpdateGameHandler>();
        services.AddScoped<DeleteGameHandler>();
        services.AddScoped<AssignGameToDeviceHandler>();
        services.AddScoped<UnassignGameFromDeviceHandler>();
        services.AddScoped<LaunchGameHandler>();
        services.AddScoped<CloseGameHandler>();

        services.AddScoped<LoginHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<CreateEmployeeHandler>();
        services.AddScoped<SetEmployeeActiveHandler>();
        services.AddScoped<ClockInHandler>();
        services.AddScoped<ClockOutHandler>();

        services.AddScoped<CloudMessageDispatcher>();
        services.AddScoped<ProcessOutboxHandler>();
        services.AddScoped<ProcessInboxHandler>();
        services.AddScoped<GetSyncStatusHandler>();
        services.AddScoped<ICloudMessageHandler, CustomerUpsertMessageHandler>();

        services.AddScoped<GetFinancialReportHandler>();

        return services;
    }
}
