using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GamePort.Cashier.Application;
using GamePort.Cashier.Common;
using GamePort.Cashier.Infrastructure;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Hosting;
using GamePort.Cashier.Services;
using GamePort.Cashier.ViewModels;
using GamePort.Cashier.Views;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GamePort.Cashier;

public partial class App : System.Windows.Application
{
    private WebApplication? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDirectory = LocalPaths.DefaultDataDirectory;

        try
        {
            LocalPaths.EnsureDataDirectory(dataDirectory);
            CopyDefaultConfiguration(dataDirectory);
            Directory.SetCurrentDirectory(dataDirectory);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = e.Args,
                ContentRootPath = dataDirectory
            });

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LocalPaths.DataDirectoryConfigKey] = dataDirectory
            });

            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));

            var useInMemory = builder.Configuration.GetValue("Database:UseInMemory", true);
            var connectionString = useInMemory
                ? null
                : builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddApplicationServices();
            builder.Services.AddInfrastructureServices(connectionString);
            builder.Services.AddCashierServer(builder.Configuration);

            RegisterClientServices(builder);

            builder.WebHost.UseUrls(PortResolver.GetAvailableUrls(builder.Configuration));

            _host = builder.Build();
            _host.UseCashierServer();
            _host.MapCashierServer();

            await ApplyMigrationsAsync(builder.Configuration);
            await _host.StartAsync();

            ShowLoginAndMainWindow();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cashier startup failed.");
            WriteStartupError(dataDirectory, ex);
            MessageBox.Show(
                $"{ex.Message}{Environment.NewLine}{Environment.NewLine}مسیر داده: {dataDirectory}",
                "GamePort Cashier - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void CopyDefaultConfiguration(string dataDirectory)
    {
        try
        {
            var source = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var target = Path.Combine(dataDirectory, "appsettings.json");

            if (File.Exists(source) && !File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not copy default configuration to {Directory}.", dataDirectory);
        }
    }

    private void RegisterClientServices(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<INavigationService>(sp =>
            new NavigationService(type => (ObservableObject)sp.GetRequiredService(type)));

        builder.Services.AddSingleton<ISessionContext, SessionContext>();

        builder.Services.AddHttpClient<IAutoUpdater, GitHubAutoUpdater>((_, http) =>
        {
            http.BaseAddress = new Uri("https://api.github.com/");
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GamePort-Cashier/1.0");
        });

        builder.Services.AddHttpClient<IApiClient, ApiClient>((_, http) =>
        {
            http.BaseAddress = new Uri(ResolveApiBaseUrl(builder.Configuration));
            http.Timeout = TimeSpan.FromSeconds(20);
        });

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginWindow>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<DevicesViewModel>();
        builder.Services.AddTransient<FinancialReportsViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();
    }

    private static string ResolveApiBaseUrl(IConfiguration configuration)
    {
        var configured = configuration.GetValue<string>("Api:BaseUrl");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var serverUrl = configuration.GetServerUrls().FirstOrDefault() ?? "http://0.0.0.0:5210";
        return serverUrl
            .Replace("0.0.0.0", "127.0.0.1")
            .Replace("+", "127.0.0.1")
            .Replace("*", "127.0.0.1");
    }

    private void ShowLoginAndMainWindow()
    {
        if (_host is null)
        {
            return;
        }

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        if (loginWindow.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var mainViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = mainViewModel;

        mainViewModel.LogoutRequested += () =>
        {
            mainWindow.Hide();

            var login = _host.Services.GetRequiredService<LoginWindow>();
            if (login.ShowDialog() == true)
            {
                mainWindow.Show();
            }
            else
            {
                mainWindow.Close();
                Shutdown();
            }
        };

        mainWindow.Show();
    }

    private async Task ApplyMigrationsAsync(IConfiguration configuration)
    {
        if (_host is null)
        {
            return;
        }

        using var scope = _host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CashierDbContext>();

        if (context.Database.IsRelational() && configuration.GetValue("Database:AutoMigrate", true))
        {
            await context.Database.MigrateAsync();
        }
    }

    private static void WriteStartupError(string dataDirectory, Exception exception)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(dataDirectory, "startup-error.txt"),
                exception.ToString());
        }
        catch
        {
            // Startup diagnostics must never throw.
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            await _host.DisposeAsync();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
