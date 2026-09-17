using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Devices;

public class RegisterDeviceHandler
{
    private readonly IDeviceRepository _devices;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly ISecretHasher _secretHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceHandler(
        IDeviceRepository devices,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        ISecretHasher secretHasher,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
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

        var clientIdentity = SecretGenerator.GenerateIdentity();
        var clientSecret = SecretGenerator.Generate();

        var device = new Device
        {
            Name = command.Name.Trim(),
            Type = command.Type,
            Status = DeviceStatus.Available,
            IpAddress = command.IpAddress,
            MacAddress = command.MacAddress,
            HardwareInfo = command.HardwareInfo,
            ClientIdentity = clientIdentity,
            ClientSecretHash = _secretHasher.Hash(clientSecret),
            CredentialIssuedAt = DateTime.UtcNow,
            IsConnected = false
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _devices.CreateAsync(device);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "RegisterDevice",
                EntityType = nameof(Device),
                EntityId = device.Id,
                ActorType = AuditActorType.Employee,
                Source = "Cashier",
                AfterState = JsonSerializer.Serialize(new { device.Name, device.Type, device.IpAddress })
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
}
