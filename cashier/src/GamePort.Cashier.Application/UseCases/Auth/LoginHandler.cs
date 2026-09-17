using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Auth;

public class LoginHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly ISecretHasher _secretHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public LoginHandler(
        IEmployeeRepository employees,
        ISecretHasher secretHasher,
        ITokenService tokenService,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _secretHasher = secretHasher;
        _tokenService = tokenService;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LoginResult>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            return Result<LoginResult>.Failure("نام کاربری و رمز عبور الزامی است.");
        }

        var employee = await _employees.GetByUsernameAsync(command.Username.Trim());
        if (employee is null || !employee.IsActive)
        {
            return Result<LoginResult>.Failure("نام کاربری یا رمز عبور نادرست است.");
        }

        if (!_secretHasher.Verify(command.Password, employee.PasswordHash))
        {
            return Result<LoginResult>.Failure("نام کاربری یا رمز عبور نادرست است.");
        }

        var token = _tokenService.CreateToken(employee);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            employee.LastLoginAt = DateTime.UtcNow;
            await _employees.UpdateAsync(employee);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "EmployeeLogin",
                EntityType = nameof(Employee),
                EntityId = employee.Id,
                ActorId = employee.Id,
                ActorType = AuditActorType.Employee,
                ActorName = employee.Name,
                Source = "Cashier",
                EmployeeId = employee.Id
            });
        }, cancellationToken);

        return Result<LoginResult>.Success(new LoginResult(
            token.Token, token.ExpiresAt, employee.Id, employee.Name, employee.Username, employee.Role));
    }
}

public class ChangePasswordHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly ISecretHasher _secretHasher;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordHandler(
        IEmployeeRepository employees,
        ISecretHasher secretHasher,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _secretHasher = secretHasher;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        var employee = await _employees.GetByIdAsync(command.EmployeeId);
        if (employee is null)
        {
            return Result.Failure("کارمند یافت نشد.");
        }

        if (!_secretHasher.Verify(command.CurrentPassword, employee.PasswordHash))
        {
            return Result.Failure("رمز عبور فعلی نادرست است.");
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 6)
        {
            return Result.Failure("رمز عبور جدید باید حداقل ۶ کاراکتر باشد.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            employee.PasswordHash = _secretHasher.Hash(command.NewPassword);
            await _employees.UpdateAsync(employee);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "ChangePassword",
                EntityType = nameof(Employee),
                EntityId = employee.Id,
                ActorId = employee.Id,
                ActorType = AuditActorType.Employee,
                ActorName = employee.Name,
                Source = "Cashier",
                EmployeeId = employee.Id
            });
        }, cancellationToken);

        return Result.Success();
    }
}
