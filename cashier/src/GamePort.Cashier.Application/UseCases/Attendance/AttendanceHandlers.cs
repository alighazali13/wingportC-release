using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Attendance;

public record ClockInCommand(Guid EmployeeId, string? Notes);
public record ClockOutCommand(Guid EmployeeId);

public record AttendanceResult(
    Guid AttendanceId,
    Guid EmployeeId,
    DateTime ClockInAt,
    DateTime? ClockOutAt);

public class ClockInHandler
{
    private readonly IAttendanceRepository _attendance;
    private readonly IEmployeeRepository _employees;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public ClockInHandler(
        IAttendanceRepository attendance,
        IEmployeeRepository employees,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _attendance = attendance;
        _employees = employees;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceResult>> HandleAsync(ClockInCommand command, CancellationToken cancellationToken = default)
    {
        var employee = await _employees.GetByIdAsync(command.EmployeeId);
        if (employee is null)
        {
            return Result<AttendanceResult>.Failure("کارمند یافت نشد.");
        }

        var open = await _attendance.GetOpenByEmployeeAsync(command.EmployeeId);
        if (open is not null)
        {
            return Result<AttendanceResult>.Failure("این کارمند در حال حاضر حضور باز دارد.");
        }

        var record = new AttendanceRecord
        {
            EmployeeId = command.EmployeeId,
            ClockInAt = DateTime.UtcNow,
            Notes = command.Notes
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _attendance.CreateAsync(record);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "ClockIn",
                EntityType = nameof(AttendanceRecord),
                EntityId = record.Id,
                ActorId = employee.Id,
                ActorType = AuditActorType.Employee,
                ActorName = employee.Name,
                Source = "Cashier",
                EmployeeId = employee.Id
            });
        }, cancellationToken);

        return Result<AttendanceResult>.Success(new AttendanceResult(record.Id, employee.Id, record.ClockInAt, null));
    }
}

public class ClockOutHandler
{
    private readonly IAttendanceRepository _attendance;
    private readonly IEmployeeRepository _employees;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public ClockOutHandler(
        IAttendanceRepository attendance,
        IEmployeeRepository employees,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _attendance = attendance;
        _employees = employees;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceResult>> HandleAsync(ClockOutCommand command, CancellationToken cancellationToken = default)
    {
        var record = await _attendance.GetOpenByEmployeeAsync(command.EmployeeId);
        if (record is null)
        {
            return Result<AttendanceResult>.Failure("حضور بازی برای این کارمند یافت نشد.");
        }

        var clockOutAt = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            record.ClockOutAt = clockOutAt;
            await _attendance.UpdateAsync(record);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "ClockOut",
                EntityType = nameof(AttendanceRecord),
                EntityId = record.Id,
                ActorId = command.EmployeeId,
                ActorType = AuditActorType.Employee,
                Source = "Cashier",
                EmployeeId = command.EmployeeId,
                AfterState = JsonSerializer.Serialize(new { record.ClockOutAt })
            });
        }, cancellationToken);

        return Result<AttendanceResult>.Success(new AttendanceResult(record.Id, command.EmployeeId, record.ClockInAt, clockOutAt));
    }
}
