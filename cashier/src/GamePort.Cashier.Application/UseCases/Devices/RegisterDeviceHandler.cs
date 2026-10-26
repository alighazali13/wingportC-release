using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Devices;

public class RegisterDeviceHandler
{
    private readonly IDeviceRepository _devices;
    private readonly IZoneRepository _zones;
    private readonly IPricingGroupRepository _pricingGroups;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly ISecretHasher _secretHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceHandler(
        IDeviceRepository devices,
        IZoneRepository zones,
        IPricingGroupRepository pricingGroups,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        ISecretHasher secretHasher,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _zones = zones;
        _pricingGroups = pricingGroups;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _secretHasher = secretHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RegisterDeviceResult>> HandleAsync(RegisterDeviceCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<RegisterDeviceResult>.Failure("نام دستگاه الزامی است.");
        }

        if (command.Type == DeviceType.GamingPC)
        {
            if (string.IsNullOrWhiteSpace(command.IpAddress))
            {
                return Result<RegisterDeviceResult>.Failure("آدرس IP برای رایانه گیمینگ الزامی است.");
            }

            if (!IsValidIp(command.IpAddress))
            {
                return Result<RegisterDeviceResult>.Failure("آدرس IP نامعتبر است.");
            }

            if (command.ConsoleModel is not null || command.ControllerCount.HasValue || command.MaxPlayers.HasValue)
            {
                return Result<RegisterDeviceResult>.Failure("فیلدهای مخصوص کنسول برای رایانه گیمینگ قابل استفاده نیستند.");
            }
        }
        else if (command.Type == DeviceType.PlayStation)
        {
            if (!string.IsNullOrWhiteSpace(command.IpAddress) && !IsValidIp(command.IpAddress))
            {
                return Result<RegisterDeviceResult>.Failure("آدرس IP نامعتبر است.");
            }
        }
        else
        {
            return Result<RegisterDeviceResult>.Failure("نوع دستگاه نامعتبر است.");
        }

        var zoneError = await DeviceReferenceValidator.ValidateZoneAsync(_zones, command.ZoneId, cancellationToken);
        if (zoneError is not null)
        {
            return Result<RegisterDeviceResult>.Failure(zoneError);
        }

        var pricingGroupError = await DeviceReferenceValidator.ValidatePricingGroupAsync(
            _pricingGroups, command.PricingGroupId, command.Type, cancellationToken);
        if (pricingGroupError is not null)
        {
            return Result<RegisterDeviceResult>.Failure(pricingGroupError);
        }

        var isPc = command.Type == DeviceType.GamingPC;

        string? clientIdentity = null;
        string? clientSecretHash = null;
        var clientSecret = string.Empty;
        DateTime? credentialIssuedAt = null;

        if (isPc)
        {
            clientIdentity = SecretGenerator.GenerateIdentity();
            clientSecret = SecretGenerator.Generate();
            clientSecretHash = _secretHasher.Hash(clientSecret);
            credentialIssuedAt = DateTime.UtcNow;
        }

        var device = new Device
        {
            Name = command.Name.Trim(),
            Type = command.Type,
            // Legacy status kept for backward compatibility.
            Status = DeviceStatus.Available,
            IpAddress = string.IsNullOrWhiteSpace(command.IpAddress) ? null : command.IpAddress.Trim(),
            MacAddress = command.MacAddress,
            HardwareInfo = command.HardwareInfo,
            ClientIdentity = clientIdentity,
            ClientSecretHash = clientSecretHash,
            CredentialIssuedAt = credentialIssuedAt,
            IsConnected = false,
            ZoneId = command.ZoneId,
            PricingGroupId = command.PricingGroupId,
            DisplayOrder = command.DisplayOrder,
            IsActive = true,
            OperationalStatus = OperationalStatus.Available,
            ConnectionMode = isPc ? ConnectionMode.Client : ConnectionMode.Manual,
            ConnectivityStatus = isPc ? ConnectivityStatus.Disconnected : ConnectivityStatus.NotApplicable,
            ConsoleModel = isPc ? null : command.ConsoleModel,
            ControllerCount = isPc ? null : command.ControllerCount,
            MaxPlayers = isPc ? null : command.MaxPlayers
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _devices.CreateAsync(device);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "RegisterDevice",
                EntityType = nameof(Device),
                EntityId = device.Id,
                ActorId = command.ActorEmployeeId,
                ActorType = command.ActorEmployeeId.HasValue ? AuditActorType.Employee : AuditActorType.System,
                EmployeeId = command.ActorEmployeeId,
                Source = "Cashier",
                DeviceId = device.Id,
                AfterState = JsonSerializer.Serialize(new
                {
                    device.Name,
                    Type = device.Type.ToString(),
                    device.IpAddress,
                    device.MacAddress,
                    device.ZoneId,
                    device.PricingGroupId,
                    device.DisplayOrder,
                    device.ConsoleModel,
                    device.ControllerCount,
                    device.MaxPlayers,
                    ConnectionMode = device.ConnectionMode.ToString(),
                    ConnectivityStatus = device.ConnectivityStatus.ToString(),
                    OperationalStatus = device.OperationalStatus.ToString(),
                    device.IsActive
                })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "device.registered",
                IdempotencyKey = $"device.registered:{device.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    DeviceId = device.Id,
                    device.Name,
                    Type = device.Type.ToString(),
                    device.IpAddress,
                    device.ClientIdentity
                })
            });
        }, cancellationToken);

        return Result<RegisterDeviceResult>.Success(
            new RegisterDeviceResult(device, clientSecret));
    }

    internal static bool IsValidIp(string value)
    {
        if (!IPAddress.TryParse(value.Trim(), out var address))
        {
            return false;
        }

        return address.AddressFamily == AddressFamily.InterNetwork
               || address.AddressFamily == AddressFamily.InterNetworkV6;
    }
}
