using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Employees;

public class CreateEmployeeHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly ISecretHasher _secretHasher;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeHandler(
        IEmployeeRepository employees,
        ISecretHasher secretHasher,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _secretHasher = secretHasher;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeResult>> HandleAsync(CreateEmployeeCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Username))
        {
            return Result<EmployeeResult>.Failure("نام و نام کاربری الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 6)
        {
            return Result<EmployeeResult>.Failure("رمز عبور باید حداقل ۶ کاراکتر باشد.");
        }

        if (!Roles.IsValid(command.Role))
        {
            return Result<EmployeeResult>.Failure("نقش انتخابی نامعتبر است.");
        }

        var username = command.Username.Trim();
        var existing = await _employees.GetByUsernameAsync(username);
        if (existing is not null)
        {
            return Result<EmployeeResult>.Failure("این نام کاربری قبلاً استفاده شده است.");
        }

        var employee = new Employee
        {
            Name = command.Name.Trim(),
            Username = username,
            PasswordHash = _secretHasher.Hash(command.Password),
            Role = command.Role,
            IsActive = true
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _employees.CreateAsync(employee);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "CreateEmployee",
                EntityType = nameof(Employee),
                EntityId = employee.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                EmployeeId = employee.Id,
                AfterState = JsonSerializer.Serialize(new { employee.Name, employee.Username, employee.Role })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "employee.created",
                IdempotencyKey = $"employee.created:{employee.Id}",
                Payload = JsonSerializer.Serialize(new { EmployeeId = employee.Id, employee.Name, employee.Username, employee.Role })
            });
        }, cancellationToken);

        return Result<EmployeeResult>.Success(new EmployeeResult(
            employee.Id, employee.Name, employee.Username, employee.Role, employee.IsActive));
    }
}

public class SetEmployeeActiveHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public SetEmployeeActiveHandler(IEmployeeRepository employees, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeResult>> HandleAsync(SetEmployeeActiveCommand command, CancellationToken cancellationToken = default)
    {
        var employee = await _employees.GetByIdAsync(command.EmployeeId);
        if (employee is null)
        {
            return Result<EmployeeResult>.Failure("کارمند یافت نشد.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            employee.IsActive = command.IsActive;
            await _employees.UpdateAsync(employee);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = command.IsActive ? "ActivateEmployee" : "DeactivateEmployee",
                EntityType = nameof(Employee),
                EntityId = employee.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                EmployeeId = employee.Id,
                AfterState = JsonSerializer.Serialize(new { employee.IsActive })
            });
        }, cancellationToken);

        return Result<EmployeeResult>.Success(new EmployeeResult(
            employee.Id, employee.Name, employee.Username, employee.Role, employee.IsActive));
    }
}
