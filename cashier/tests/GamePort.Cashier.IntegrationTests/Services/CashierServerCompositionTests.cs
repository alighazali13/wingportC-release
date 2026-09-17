using GamePort.Cashier.Application;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Infrastructure;
using GamePort.Cashier.Infrastructure.Communication.SignalR;
using GamePort.Cashier.Infrastructure.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class CashierServerCompositionTests
{
    [Fact]
    public void ServiceGraph_ResolvesAllCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddInfrastructureServices(null);
        services.AddCashierServer(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;

        Assert.NotNull(scoped.GetRequiredService<IDeviceRepository>());
        Assert.NotNull(scoped.GetRequiredService<ICustomerRepository>());
        Assert.NotNull(scoped.GetRequiredService<ISessionRepository>());
        Assert.NotNull(scoped.GetRequiredService<IPricingRuleRepository>());
        Assert.NotNull(scoped.GetRequiredService<IAuditLogRepository>());
        Assert.NotNull(scoped.GetRequiredService<IOutboxRepository>());
        Assert.NotNull(scoped.GetRequiredService<IInboxRepository>());
        Assert.NotNull(scoped.GetRequiredService<ISyncCheckpointRepository>());
        Assert.NotNull(scoped.GetRequiredService<IDeviceAuthenticator>());
        Assert.NotNull(scoped.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scoped.GetRequiredService<RegisterDeviceHandler>());
        Assert.NotNull(scoped.GetRequiredService<StartSessionHandler>());
        Assert.NotNull(scoped.GetRequiredService<EndSessionHandler>());
        Assert.NotNull(provider.GetRequiredService<IDeviceConnectionManager>());
        Assert.NotNull(provider.GetRequiredService<IDeviceCommandSender>());
        Assert.NotNull(provider.GetRequiredService<IHubContext<ClientHub, IClientHubClient>>());
    }
}
