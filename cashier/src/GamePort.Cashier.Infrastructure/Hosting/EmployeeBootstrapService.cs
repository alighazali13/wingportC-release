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
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private const int PasswordLength = 14;

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

            var password = GeneratePassword();
            var passwordHash = secretHasher.Hash(password);

            await employees.CreateAsync(new Employee
            {
                Name = "Administrator",
                Username = "admin",
                PasswordHash = passwordHash,
                Role = Roles.Admin,
                IsActive = true
            });

            var credentialsPath = Path.Combine(dataDir, "admin-credentials.dat");
            await File.WriteAllTextAsync(credentialsPath, passwordHash, stoppingToken);

            var firstRunPath = Path.Combine(dataDir, "first-run-admin.txt");
            await File.WriteAllTextAsync(
                firstRunPath,
                $"username: admin{Environment.NewLine}" +
                $"password: {password}{Environment.NewLine}{Environment.NewLine}" +
                "This file was generated on first run. Sign in, change the password, then delete this file.",
                stoppingToken);

            _logger.LogWarning("Admin account created. Login: admin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Employee bootstrap failed.");
        }
    }

    private static string GeneratePassword()
    {
        var chars = new char[PasswordLength];
        for (var i = 0; i < PasswordLength; i++)
        {
            chars[i] = PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)];
        }

        return new string(chars);
    }
}
