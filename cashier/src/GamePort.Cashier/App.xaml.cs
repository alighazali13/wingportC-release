using System.IO;
using System.Windows;
using GamePort.Cashier.Application;
using GamePort.Cashier.Common;
using GamePort.Cashier.Infrastructure;
using GamePort.Cashier.Infrastructure.Hosting;
using GamePort.Cashier.Services;
using GamePort.Cashier.ViewModels;
using GamePort.Cashier.Views;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GamePort.Cashier;

public partial class App : System.Windows.Application
{
    private static IHost? _host;
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider ServiceProvider => _serviceProvider!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var dataDir = LocalPaths.DefaultDataDirectory;

        try
        {
            LocalPaths.EnsureDataDirectory(dataDir);
            LocalPaths.InitializeConfiguration(dataDir);
            Directory.SetCurrentDirectory(dataDir);

            var urls = GetServerUrls(dataDir);

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, config) =>
                {
                    config.SetBasePath(dataDir);
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(urls);
                    webBuilder.Configure(app =>
                    {
                        app.UseMiddleware<ApiExceptionMiddleware>();
                        app.UseRouting();
                        app.UseRateLimiter();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapCashierApi();
                        });
                    });
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddApplicationServices();
                    services.AddInfrastructureServices(
                        ctx.Configuration.GetConnectionString("DefaultConnection"));
                    services.AddCashierServer(ctx.Configuration);

                    services.AddSingleton<ISessionContext, SessionContext>();
                    services.AddSingleton<IApiClient, ApiClient>(sp =>
                    {
                        var localClientUri = LocalClientUrlResolver.Resolve(urls);
                        return new ApiClient(
                            new System.Net.Http.HttpClient
                            {
                                BaseAddress = localClientUri,
                                Timeout = TimeSpan.FromSeconds(20)
                            },
                            sp.GetRequiredService<ISessionContext>());
                    });

                    services.AddSingleton<INavigationService, NavigationService>(sp =>
                        new NavigationService(type =>
                            (CommunityToolkit.Mvvm.ComponentModel.ObservableObject)sp.GetRequiredService(type)));
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<DevicesViewModel>();
                    services.AddTransient<UsersViewModel>();
                    services.AddTransient<FinancialReportsViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<LoginView>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            _serviceProvider = _host.Services;

            await _host.StartAsync();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}", "GamePort Cashier",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static string[] GetServerUrls(string dataDir)
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(dataDir)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            return config.GetServerUrls();
        }
        catch
        {
            return new[] { "http://0.0.0.0:5211" };
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            await ((IAsyncDisposable)_host).DisposeAsync();
        }

        base.OnExit(e);
    }
}
