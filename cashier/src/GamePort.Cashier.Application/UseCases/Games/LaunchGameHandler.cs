using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Contracts.Hub;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Games;

public class LaunchGameHandler
{
    private readonly IDeviceRepository _devices;
    private readonly IGameRepository _games;
    private readonly IDeviceGameRepository _deviceGames;
    private readonly ISessionRepository _sessions;
    private readonly IDeviceCommandSender _commandSender;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public LaunchGameHandler(
        IDeviceRepository devices,
        IGameRepository games,
        IDeviceGameRepository deviceGames,
        ISessionRepository sessions,
        IDeviceCommandSender commandSender,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _games = games;
        _deviceGames = deviceGames;
        _sessions = sessions;
        _commandSender = commandSender;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LaunchGameResult>> HandleAsync(LaunchGameCommand command, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return Result<LaunchGameResult>.Failure("دستگاه یافت نشد.");
        }

        var game = await _games.GetByIdAsync(command.GameId);
        if (game is null)
        {
            return Result<LaunchGameResult>.Failure("بازی یافت نشد.");
        }

        if (!game.IsActive)
        {
            return Result<LaunchGameResult>.Failure("این بازی غیرفعال است.");
        }

        if (game.SupportedDeviceType != device.Type)
        {
            return Result<LaunchGameResult>.Failure("این بازی برای نوع دستگاه پشتیبانی نمی‌شود.");
        }

        var openSession = await _sessions.GetOpenByDeviceIdAsync(device.Id);
        if (openSession is null)
        {
            return Result<LaunchGameResult>.Failure("برای اجرای بازی، دستگاه باید نشست فعال داشته باشد.");
        }

        var mapping = await _deviceGames.GetAsync(device.Id, game.Id);
        if (mapping is null)
        {
            return Result<LaunchGameResult>.Failure("این بازی روی دستگاه انتخابی نصب/اختصاص داده نشده است.");
        }

        var message = new DeviceCommandMessage
        {
            DeviceId = device.Id,
            CommandType = DeviceCommandType.LaunchGame,
            Payload = JsonSerializer.Serialize(new GameLaunchPayload
            {
                GameId = game.Id,
                Name = game.Name,
                ExecutablePath = game.ExecutablePath,
                LaunchConfiguration = game.LaunchConfiguration
            })
        };

        var delivered = await _commandSender.SendAsync(device.Id, message, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            device.CurrentGameId = game.Id;
            await _devices.UpdateAsync(device);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "LaunchGame",
                EntityType = nameof(Game),
                EntityId = game.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                DeviceId = device.Id,
                SessionId = openSession.Id,
                AfterState = JsonSerializer.Serialize(new { game.Name, Delivered = delivered })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "game.launch.requested",
                IdempotencyKey = $"game.launch:{device.Id}:{game.Id}:{message.CommandId}",
                Payload = JsonSerializer.Serialize(new
                {
                    DeviceId = device.Id,
                    GameId = game.Id,
                    SessionId = openSession.Id,
                    Delivered = delivered
                })
            });
        }, cancellationToken);

        return Result<LaunchGameResult>.Success(
            new LaunchGameResult(device.Id, game.Id, message.CommandId, delivered));
    }
}

public class CloseGameHandler
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceCommandSender _commandSender;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public CloseGameHandler(
        IDeviceRepository devices,
        IDeviceCommandSender commandSender,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _commandSender = commandSender;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CloseGameResult>> HandleAsync(CloseGameCommand command, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return Result<CloseGameResult>.Failure("دستگاه یافت نشد.");
        }

        var message = new DeviceCommandMessage
        {
            DeviceId = device.Id,
            CommandType = DeviceCommandType.CloseGame
        };

        var delivered = await _commandSender.SendAsync(device.Id, message, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            device.CurrentGameId = null;
            await _devices.UpdateAsync(device);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "CloseGame",
                EntityType = nameof(Device),
                EntityId = device.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                DeviceId = device.Id,
                AfterState = JsonSerializer.Serialize(new { Delivered = delivered })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "game.close.requested",
                IdempotencyKey = $"game.close:{device.Id}:{message.CommandId}",
                Payload = JsonSerializer.Serialize(new { DeviceId = device.Id, Delivered = delivered })
            });
        }, cancellationToken);

        return Result<CloseGameResult>.Success(new CloseGameResult(device.Id, message.CommandId, delivered));
    }
}
