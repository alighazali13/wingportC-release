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

            if (await employees.CountAsync() > 0)
            {
                return;
            }

            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var password = GeneratePassword();

            await employees.CreateAsync(new Employee
            {
                Name = "Administrator",
                Username = "admin",
                PasswordHash = secretHasher.Hash(password),
                Role = Roles.Admin,
                IsActive = true
            });

            var credentialsPath = Path.Combine(LocalPaths.Resolve(_configuration), "first-run-admin.txt");
            await File.WriteAllTextAsync(
                credentialsPath,
                $"username: admin{Environment.NewLine}" +
                $"password: {password}{Environment.NewLine}{Environment.NewLine}" +
                "This file was generated on first run. Sign in, change the password, then delete this file." +
                Environment.NewLine,
                stoppingToken);

            _logger.LogWarning(
                "No employees existed. A default administrator was created. Credentials were written to {Path}. Change the password and delete the file.",
                credentialsPath);
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
