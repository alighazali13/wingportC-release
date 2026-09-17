using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Games;

public class AssignGameToDeviceHandler
{
    private readonly IDeviceGameRepository _deviceGames;
    private readonly IDeviceRepository _devices;
    private readonly IGameRepository _games;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public AssignGameToDeviceHandler(
        IDeviceGameRepository deviceGames,
        IDeviceRepository devices,
        IGameRepository games,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _deviceGames = deviceGames;
        _devices = devices;
        _games = games;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeviceGameResult>> HandleAsync(AssignGameToDeviceCommand command, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return Result<DeviceGameResult>.Failure("دستگاه یافت نشد.");
        }

        var game = await _games.GetByIdAsync(command.GameId);
        if (game is null)
        {
            return Result<DeviceGameResult>.Failure("بازی یافت نشد.");
        }

        if (game.SupportedDeviceType != device.Type)
        {
            return Result<DeviceGameResult>.Failure("این بازی برای نوع دستگاه انتخابی پشتیبانی نمی‌شود.");
        }

        var existing = await _deviceGames.GetAsync(device.Id, game.Id);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (existing is null)
            {
                await _deviceGames.AddAsync(new DeviceGame
                {
                    DeviceId = device.Id,
                    GameId = game.Id,
                    IsInstalled = command.IsInstalled,
                    InstalledVersion = command.InstalledVersion,
                    InstalledAt = command.IsInstalled ? DateTime.UtcNow : null
                });
            }
            else
            {
                existing.IsInstalled = command.IsInstalled;
                existing.InstalledVersion = command.InstalledVersion;
                existing.InstalledAt = command.IsInstalled ? (existing.InstalledAt ?? DateTime.UtcNow) : null;
                await _deviceGames.UpdateAsync(existing);
            }

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "AssignGameToDevice",
                EntityType = nameof(DeviceGame),
                EntityId = game.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                DeviceId = device.Id,
                AfterState = JsonSerializer.Serialize(new { game.Name, command.IsInstalled, command.InstalledVersion })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "device.game.assigned",
                IdempotencyKey = $"device.game.assigned:{device.Id}:{game.Id}",
                Payload = JsonSerializer.Serialize(new { DeviceId = device.Id, GameId = game.Id, command.IsInstalled })
            });
        }, cancellationToken);

        return Result<DeviceGameResult>.Success(new DeviceGameResult(device.Id, game.Id, game.Name, command.IsInstalled));
    }
}

public class UnassignGameFromDeviceHandler
{
    private readonly IDeviceGameRepository _deviceGames;
    private readonly IDeviceRepository _devices;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public UnassignGameFromDeviceHandler(
        IDeviceGameRepository deviceGames,
        IDeviceRepository devices,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _deviceGames = deviceGames;
        _devices = devices;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeviceGameResult>> HandleAsync(UnassignGameFromDeviceCommand command, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return Result<DeviceGameResult>.Failure("دستگاه یافت نشد.");
        }

        var existing = await _deviceGames.GetAsync(command.DeviceId, command.GameId);
        if (existing is null)
        {
            return Result<DeviceGameResult>.Failure("این بازی به دستگاه اختصاص داده نشده است.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _deviceGames.DeleteAsync(command.DeviceId, command.GameId);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "UnassignGameFromDevice",
                EntityType = nameof(DeviceGame),
                EntityId = command.GameId,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                DeviceId = device.Id
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "device.game.unassigned",
                IdempotencyKey = $"device.game.unassigned:{device.Id}:{command.GameId}",
                Payload = JsonSerializer.Serialize(new { DeviceId = device.Id, GameId = command.GameId })
            });
        }, cancellationToken);

        return Result<DeviceGameResult>.Success(new DeviceGameResult(device.Id, command.GameId, string.Empty, false));
    }
}
