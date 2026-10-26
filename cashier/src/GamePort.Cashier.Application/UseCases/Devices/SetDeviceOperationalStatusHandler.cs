using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Domain.Exceptions;

namespace GamePort.Cashier.Application.UseCases.Devices;

public record SetDeviceOperationalStatusCommand(
    Guid DeviceId,
    OperationalStatus OperationalStatus,
    string? MaintenanceNotes,
    uint Xmin,
    Guid? ActorEmployeeId = null);

/// <summary>
/// PATCH manual operational status: Available ↔ Maintenance only (InUse is session-lifecycle owned).
/// Entering Maintenance requires no open session; ConnectivityStatus and IsActive are never
/// changed as a side effect. LastMaintenanceAt policy: stamped with UtcNow on every successful
/// manual transition (both entering and exiting Maintenance), recording the most recent
/// maintenance action. Legacy Status is synchronized via DeviceStateTransitions.
/// </summary>
public class SetDeviceOperationalStatusHandler
{
    private readonly IDeviceRepository _devices;
    private readonly ISessionRepository _sessions;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public SetDeviceOperationalStatusHandler(
        IDeviceRepository devices,
        ISessionRepository sessions,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _sessions = sessions;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeviceLifecycleResult> HandleAsync(SetDeviceOperationalStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OperationalStatus is not (OperationalStatus.Available or OperationalStatus.Maintenance))
        {
            return DeviceLifecycleResult.Validation("وضعیت عملیاتی فقط می‌تواند Available یا Maintenance باشد. InUse توسط چرخه نشست مدیریت می‌شود.");
        }

        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return DeviceLifecycleResult.NotFound();
        }

        if (device.xmin != command.Xmin)
        {
            return DeviceLifecycleResult.Stale(DeviceConcurrencyGuard.StaleVersionMessage);
        }

        if (command.OperationalStatus == OperationalStatus.Maintenance)
        {
            if (device.OperationalStatus == OperationalStatus.InUse)
            {
                return DeviceLifecycleResult.Validation("دستگاه در حال استفاده است؛ ابتدا نشست را پایان دهید.");
            }

            if (device.OperationalStatus == OperationalStatus.Maintenance)
            {
                return DeviceLifecycleResult.Validation("دستگاه هم‌اکنون در وضعیت تعمیرات است.");
            }

            var openSession = await _sessions.GetOpenByDeviceIdAsync(device.Id);
            if (openSession is not null)
            {
                return DeviceLifecycleResult.Conflict("دستگاه نشست فعال دارد؛ ورود به تعمیرات مجاز نیست.");
            }
        }
        else // Available
        {
            if (device.OperationalStatus == OperationalStatus.InUse)
            {
                return DeviceLifecycleResult.Validation("وضعیت InUse فقط توسط چرخه نشست تغییر می‌کند.");
            }

            if (device.OperationalStatus == OperationalStatus.Available)
            {
                return DeviceLifecycleResult.Validation("دستگاه هم‌اکنون در وضعیت آماده است.");
            }
        }

        var beforeState = JsonSerializer.Serialize(new
        {
            OperationalStatus = device.OperationalStatus.ToString(),
            device.MaintenanceNotes,
            device.LastMaintenanceAt
        });

        var connectivityBefore = device.ConnectivityStatus;
        var isActiveBefore = device.IsActive;

        if (command.OperationalStatus == OperationalStatus.Maintenance)
        {
            DeviceStateTransitions.MarkMaintenanceEntered(device);
        }
        else
        {
            DeviceStateTransitions.MarkMaintenanceExited(device);
        }

        device.MaintenanceNotes = command.MaintenanceNotes;
        device.LastMaintenanceAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await _devices.UpdateAsync(device);

                await _auditLogs.AddAsync(new AuditLog
                {
                    Action = "SetDeviceOperationalStatus",
                    EntityType = nameof(Device),
                    EntityId = device.Id,
                    ActorId = command.ActorEmployeeId,
                    ActorType = command.ActorEmployeeId.HasValue ? AuditActorType.Employee : AuditActorType.System,
                    EmployeeId = command.ActorEmployeeId,
                    Source = "Cashier",
                    DeviceId = device.Id,
                    BeforeState = beforeState,
                    AfterState = JsonSerializer.Serialize(new
                    {
                        OperationalStatus = device.OperationalStatus.ToString(),
                        device.MaintenanceNotes,
                        device.LastMaintenanceAt
                    })
                });
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return DeviceLifecycleResult.Stale(DeviceConcurrencyGuard.StaleVersionMessage);
        }

        // Invariant: manual maintenance transitions never touch connectivity or activation.
        if (device.ConnectivityStatus != connectivityBefore || device.IsActive != isActiveBefore)
        {
            throw new InvalidOperationException("Operational status transition must not change ConnectivityStatus or IsActive.");
        }

        return DeviceLifecycleResult.Success(device);
    }
}
