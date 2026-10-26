using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Domain.Exceptions;

namespace GamePort.Cashier.Application.UseCases.Devices;

public record SetDeviceActivationCommand(
    Guid DeviceId,
    bool IsActive,
    uint Xmin,
    Guid? ActorEmployeeId = null);

/// <summary>
/// PATCH device activation (IsActive). Deactivation is allowed while a session is active —
/// the running session continues, but new sessions/reservations are rejected by the
/// eligibility guards. Reactivation never changes OperationalStatus or ConnectivityStatus.
/// </summary>
public class SetDeviceActivationHandler
{
    private readonly IDeviceRepository _devices;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public SetDeviceActivationHandler(
        IDeviceRepository devices,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeviceLifecycleResult> HandleAsync(SetDeviceActivationCommand command, CancellationToken cancellationToken = default)
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

        var beforeState = JsonSerializer.Serialize(new { device.IsActive });

        // Only IsActive changes. OperationalStatus, ConnectivityStatus, legacy Status,
        // IsConnected, LastHeartbeat and any open session are intentionally untouched.
        device.IsActive = command.IsActive;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await _devices.UpdateAsync(device);

                await _auditLogs.AddAsync(new AuditLog
                {
                    Action = command.IsActive ? "ActivateDevice" : "DeactivateDevice",
                    EntityType = nameof(Device),
                    EntityId = device.Id,
                    ActorId = command.ActorEmployeeId,
                    ActorType = command.ActorEmployeeId.HasValue ? AuditActorType.Employee : AuditActorType.System,
                    EmployeeId = command.ActorEmployeeId,
                    Source = "Cashier",
                    DeviceId = device.Id,
                    BeforeState = beforeState,
                    AfterState = JsonSerializer.Serialize(new { device.IsActive })
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
