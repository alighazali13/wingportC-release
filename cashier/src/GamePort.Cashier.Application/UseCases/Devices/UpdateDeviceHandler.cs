using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Domain.Exceptions;

namespace GamePort.Cashier.Application.UseCases.Devices;

public class UpdateDeviceHandler
{
    private readonly IDeviceRepository _devices;
    private readonly IZoneRepository _zones;
    private readonly IPricingGroupRepository _pricingGroups;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDeviceHandler(
        IDeviceRepository devices,
        IZoneRepository zones,
        IPricingGroupRepository pricingGroups,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _zones = zones;
        _pricingGroups = pricingGroups;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateDeviceResult> HandleAsync(UpdateDeviceCommand command, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return UpdateDeviceResult.NotFound();
        }

        // Optimistic concurrency pre-check: submitted xmin must match the loaded database version.
        if (device.xmin != command.Xmin)
        {
            return UpdateDeviceResult.Stale(
                "نسخه ارسال‌شده منسوخ شده است؛ دستگاه توسط کاربر دیگری تغییر کرده است. لطفاً دوباره بارگذاری کنید.");
        }

        var zoneError = await DeviceReferenceValidator.ValidateZoneAsync(_zones, command.ZoneId, cancellationToken);
        if (zoneError is not null)
        {
            return UpdateDeviceResult.Validation(zoneError);
        }

        var pricingGroupError = await DeviceReferenceValidator.ValidatePricingGroupAsync(
            _pricingGroups, command.PricingGroupId, device.Type, cancellationToken);
        if (pricingGroupError is not null)
        {
            return UpdateDeviceResult.Validation(pricingGroupError);
        }

        if (device.Type == DeviceType.GamingPC)
        {
            if (string.IsNullOrWhiteSpace(command.IpAddress))
            {
                return UpdateDeviceResult.Validation("آدرس IP برای رایانه گیمینگ الزامی است.");
            }

            if (!RegisterDeviceHandler.IsValidIp(command.IpAddress))
            {
                return UpdateDeviceResult.Validation("آدرس IP نامعتبر است.");
            }

            if (command.ConsoleModel is not null || command.ControllerCount.HasValue || command.MaxPlayers.HasValue)
            {
                return UpdateDeviceResult.Validation("فیلدهای مخصوص کنسول برای رایانه گیمینگ قابل استفاده نیستند.");
            }
        }
        else if (device.Type == DeviceType.PlayStation)
        {
            if (!string.IsNullOrWhiteSpace(command.IpAddress) && !RegisterDeviceHandler.IsValidIp(command.IpAddress))
            {
                return UpdateDeviceResult.Validation("آدرس IP نامعتبر است.");
            }
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return UpdateDeviceResult.Validation("نام دستگاه الزامی است.");
        }

        // Snapshot before-state for audit (no credentials or auth material).
        var beforeState = JsonSerializer.Serialize(new
        {
            device.Name,
            device.IpAddress,
            device.MacAddress,
            device.ZoneId,
            device.PricingGroupId,
            device.DisplayOrder,
            device.MaintenanceNotes,
            device.ConsoleModel,
            device.ControllerCount,
            device.MaxPlayers
        });

        // Apply only permitted changes. Type, ClientIdentity, ClientSecretHash, ConnectionMode,
        // IsActive, OperationalStatus, ConnectivityStatus, legacy Status, IsConnected,
        // LastHeartbeat and CurrentGameId are intentionally untouched.
        // The tracked entity's xmin OriginalValue stays as loaded, so SaveChanges
        // enforces PostgreSQL xmin optimistic concurrency.
        device.Name = command.Name.Trim();
        device.IpAddress = string.IsNullOrWhiteSpace(command.IpAddress) ? null : command.IpAddress.Trim();
        device.MacAddress = command.MacAddress;
        device.ZoneId = command.ZoneId;
        device.PricingGroupId = command.PricingGroupId;
        device.DisplayOrder = command.DisplayOrder;
        device.MaintenanceNotes = command.MaintenanceNotes;

        if (device.Type == DeviceType.PlayStation)
        {
            device.ConsoleModel = command.ConsoleModel;
            device.ControllerCount = command.ControllerCount;
            device.MaxPlayers = command.MaxPlayers;
        }

        var afterState = JsonSerializer.Serialize(new
        {
            device.Name,
            device.IpAddress,
            device.MacAddress,
            device.ZoneId,
            device.PricingGroupId,
            device.DisplayOrder,
            device.MaintenanceNotes,
            device.ConsoleModel,
            device.ControllerCount,
            device.MaxPlayers
        });

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await _devices.UpdateAsync(device);

                await _auditLogs.AddAsync(new AuditLog
                {
                    Action = "UpdateDevice",
                    EntityType = nameof(Device),
                    EntityId = device.Id,
                    ActorId = command.ActorEmployeeId,
                    ActorType = command.ActorEmployeeId.HasValue ? AuditActorType.Employee : AuditActorType.System,
                    EmployeeId = command.ActorEmployeeId,
                    Source = "Cashier",
                    DeviceId = device.Id,
                    BeforeState = beforeState,
                    AfterState = afterState
                });
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return UpdateDeviceResult.Stale(
                "نسخه ارسال‌شده منسوخ شده است؛ دستگاه توسط کاربر دیگری تغییر کرده است. لطفاً دوباره بارگذاری کنید.");
        }

        return UpdateDeviceResult.Success(device);
    }
}
