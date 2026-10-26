using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Domain.Exceptions;

namespace GamePort.Cashier.Application.UseCases.Devices;

public record DeleteDeviceCommand(
    Guid DeviceId,
    uint Xmin,
    Guid? ActorEmployeeId = null);

/// <summary>
/// Soft-deletes a device with xmin optimistic concurrency.
/// Rejected when an open session (Active/Paused) exists or a future Reserved
/// reservation exists for the device. Historical sessions, reservations, payments
/// and audit records are always preserved — the row is never physically deleted.
/// </summary>
public class DeleteDeviceHandler
{
    private readonly IDeviceRepository _devices;
    private readonly ISessionRepository _sessions;
    private readonly IReservationRepository _reservations;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDeviceHandler(
        IDeviceRepository devices,
        ISessionRepository sessions,
        IReservationRepository reservations,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _sessions = sessions;
        _reservations = reservations;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeviceLifecycleResult> HandleAsync(DeleteDeviceCommand command, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return DeviceLifecycleResult.NotFound();
        }

        if (device.xmin != command.Xmin)
        {
            return DeviceLifecycleResult.Stale(DeviceConcurrencyGuard.StaleVersionMessage);
        }

        var openSession = await _sessions.GetOpenByDeviceIdAsync(device.Id);
        if (openSession is not null)
        {
            return DeviceLifecycleResult.Conflict("دستگاه نشست فعال دارد؛ حذف مجاز نیست.");
        }

        var upcoming = await _reservations.GetUpcomingAsync(DateTime.UtcNow);
        if (upcoming.Any(r => r.DeviceId == device.Id))
        {
            return DeviceLifecycleResult.Conflict("دستگاه رزرو آینده دارد؛ حذف مجاز نیست.");
        }

        var beforeState = JsonSerializer.Serialize(new
        {
            device.Name,
            Type = device.Type.ToString(),
            device.IsDeleted
        });

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                // Soft delete only — historical rows (sessions, reservations, payments,
                // audit logs) keep their FK references and remain queryable.
                device.IsDeleted = true;
                device.IsActive = false;
                await _devices.UpdateAsync(device);

                await _auditLogs.AddAsync(new AuditLog
                {
                    Action = "DeleteDevice",
                    EntityType = nameof(Device),
                    EntityId = device.Id,
                    ActorId = command.ActorEmployeeId,
                    ActorType = command.ActorEmployeeId.HasValue ? AuditActorType.Employee : AuditActorType.System,
                    EmployeeId = command.ActorEmployeeId,
                    Source = "Cashier",
                    DeviceId = device.Id,
                    BeforeState = beforeState,
                    AfterState = JsonSerializer.Serialize(new { device.IsDeleted })
                });
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return DeviceLifecycleResult.Stale(DeviceConcurrencyGuard.StaleVersionMessage);
        }

        return DeviceLifecycleResult.Success(device);
    }
}
