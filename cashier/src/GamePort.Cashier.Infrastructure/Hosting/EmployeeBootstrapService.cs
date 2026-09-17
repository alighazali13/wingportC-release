using System.Security.Cryptography;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GamePort.Cashier.Infrastructure.Hosting;

public class EmployeeBootstrapService : BackgroundService
{
    public const string DefaultUsername = "admin";
    public const string DefaultPassword = "13132525Ac@";
    private const string FirstRunFileName = "first-run-admin.txt";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmployeeBootstrapService> _logger;

    public EmployeeBootstrapService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<EmployeeBootstrapService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var dataDir = LocalPaths.Resolve(_configuration);

            if (await employees.CountAsync() > 0)
            {
                return;
            }

            var passwordHash = secretHasher.Hash(DefaultPassword);

            await employees.CreateAsync(new Employee
            {
                Name = "Administrator",
                Username = DefaultUsername,
                PasswordHash = passwordHash,
                Role = Roles.Admin,
                IsActive = true
            });

            var firstRunPath = Path.Combine(dataDir, FirstRunFileName);
            await File.WriteAllTextAsync(firstRunPath,
                $"username: {DefaultUsername}{Environment.NewLine}" +
                $"password: {DefaultPassword}{Environment.NewLine}{Environment.NewLine}" +
                "First-run credentials. Sign in and change the password, then delete this file.",
                stoppingToken);

            _logger.LogWarning("Admin account created: username={Username}", DefaultUsername);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Employee bootstrap failed.");
        }
    }
}
