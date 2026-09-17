using System.IdentityModel.Tokens.Jwt;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Application.UseCases.Attendance;
using GamePort.Cashier.Application.UseCases.Auth;
using GamePort.Cashier.Application.UseCases.Employees;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Security;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class AuthTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public CashierDbContext Context { get; }
        public EmployeeRepository Employees { get; }
        public AttendanceRepository Attendance { get; }
        public AuditLogRepository AuditLogs { get; }
        public OutboxRepository Outbox { get; }
        public UnitOfWork UnitOfWork { get; }
        public Pbkdf2SecretHasher Hasher { get; } = new();
        public JwtTokenService TokenService { get; }

        private Fixture(CashierDbContext context)
        {
            Context = context;
            Employees = new EmployeeRepository(context);
            Attendance = new AttendanceRepository(context);
            AuditLogs = new AuditLogRepository(context);
            Outbox = new OutboxRepository(context);
            UnitOfWork = new UnitOfWork(context);

            var signingKey = new SymmetricSecurityKey(new byte[64]);
            TokenService = new JwtTokenService(
                Options.Create(new JwtOptions()),
                new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CashierDbContext>()
                .UseInMemoryDatabase($"wingport-auth-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public CreateEmployeeHandler CreateEmployeeHandler() =>
            new(Employees, Hasher, AuditLogs, Outbox, UnitOfWork);

        public LoginHandler LoginHandler() =>
            new(Employees, Hasher, TokenService, AuditLogs, UnitOfWork);

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task CreateEmployee_ThenLogin_Succeeds()
    {
        await using var fixture = Fixture.Create();

        var create = await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza", "reza", "secret123", Roles.Operator, "admin"));

        Assert.True(create.IsSuccess);

        var login = await fixture.LoginHandler().HandleAsync(new LoginCommand("reza", "secret123"));

        Assert.True(login.IsSuccess);
        Assert.Equal(Roles.Operator, login.Value!.Role);
        Assert.False(string.IsNullOrWhiteSpace(login.Value.Token));
    }

    [Fact]
    public async Task Login_Fails_WithWrongPassword()
    {
        await using var fixture = Fixture.Create();
        await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza", "reza", "secret123", Roles.Operator, "admin"));

        var login = await fixture.LoginHandler().HandleAsync(new LoginCommand("reza", "wrong"));

        Assert.False(login.IsSuccess);
    }

    [Fact]
    public async Task Login_Fails_ForInactiveEmployee()
    {
        await using var fixture = Fixture.Create();
        var create = await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza", "reza", "secret123", Roles.Operator, "admin"));

        var deactivate = new SetEmployeeActiveHandler(fixture.Employees, fixture.AuditLogs, fixture.UnitOfWork);
        await deactivate.HandleAsync(new SetEmployeeActiveCommand(create.Value!.EmployeeId, false, "admin"));

        var login = await fixture.LoginHandler().HandleAsync(new LoginCommand("reza", "secret123"));

        Assert.False(login.IsSuccess);
    }

    [Fact]
    public async Task CreateEmployee_Fails_ForDuplicateUsername()
    {
        await using var fixture = Fixture.Create();
        await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza", "reza", "secret123", Roles.Operator, "admin"));

        var duplicate = await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza 2", "reza", "secret456", Roles.Operator, "admin"));

        Assert.False(duplicate.IsSuccess);
    }

    [Fact]
    public async Task CreateEmployee_Fails_ForInvalidRole()
    {
        await using var fixture = Fixture.Create();

        var result = await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza", "reza", "secret123", "SuperUser", "admin"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Token_ContainsRoleAndPermissionClaims()
    {
        var signingKey = new SymmetricSecurityKey(new byte[64]);
        var service = new JwtTokenService(
            Options.Create(new JwtOptions()),
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var employee = new Employee { Id = Guid.NewGuid(), Name = "Admin", Username = "admin", Role = Roles.Admin };
        var token = service.CreateToken(employee);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);
        var permissions = parsed.Claims.Where(c => c.Type == JwtTokenService.PermissionClaim).Select(c => c.Value).ToList();

        Assert.Contains(Roles.Admin, parsed.Claims.Where(c => c.Type == JwtTokenService.RoleClaim).Select(c => c.Value));
        Assert.Contains(Permissions.EmployeesManage, permissions);
        Assert.Equal(Permissions.All.Count, permissions.Count);
    }

    [Fact]
    public void OperatorRole_DoesNotIncludeSensitivePermissions()
    {
        var permissions = Roles.GetPermissions(Roles.Operator);

        Assert.DoesNotContain(Permissions.EmployeesManage, permissions);
        Assert.DoesNotContain(Permissions.PricingManage, permissions);
        Assert.DoesNotContain(Permissions.WalletRefund, permissions);
        Assert.Contains(Permissions.SessionsStart, permissions);
    }

    [Fact]
    public async Task Attendance_ClockInThenClockOut_WorksAndPreventsDoubleClockIn()
    {
        await using var fixture = Fixture.Create();
        var create = await fixture.CreateEmployeeHandler().HandleAsync(
            new CreateEmployeeCommand("Reza", "reza", "secret123", Roles.Operator, "admin"));
        var employeeId = create.Value!.EmployeeId;

        var clockIn = new ClockInHandler(fixture.Attendance, fixture.Employees, fixture.AuditLogs, fixture.UnitOfWork);
        var clockOut = new ClockOutHandler(fixture.Attendance, fixture.Employees, fixture.AuditLogs, fixture.UnitOfWork);

        var first = await clockIn.HandleAsync(new ClockInCommand(employeeId, null));
        Assert.True(first.IsSuccess);

        var second = await clockIn.HandleAsync(new ClockInCommand(employeeId, null));
        Assert.False(second.IsSuccess);

        var outResult = await clockOut.HandleAsync(new ClockOutCommand(employeeId));
        Assert.True(outResult.IsSuccess);
        Assert.NotNull(outResult.Value!.ClockOutAt);
    }
}
