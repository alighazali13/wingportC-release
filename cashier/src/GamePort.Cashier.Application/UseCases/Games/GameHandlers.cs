using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Games;

public class CreateGameHandler
{
    private readonly IGameRepository _games;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGameHandler(IGameRepository games, IAuditLogRepository auditLogs, IOutboxRepository outbox, IUnitOfWork unitOfWork)
    {
        _games = games;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GameResult>> HandleAsync(CreateGameCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<GameResult>.Failure("نام بازی الزامی است.");
        }

        var game = new Game
        {
            Name = command.Name.Trim(),
            Version = command.Version,
            ExecutablePath = command.ExecutablePath,
            IconPath = command.IconPath,
            SupportedDeviceType = command.SupportedDeviceType,
            LaunchConfiguration = command.LaunchConfiguration,
            IsActive = true
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _games.CreateAsync(game);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "CreateGame",
                EntityType = nameof(Game),
                EntityId = game.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                AfterState = JsonSerializer.Serialize(new { game.Name, game.SupportedDeviceType, game.ExecutablePath })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "game.created",
                IdempotencyKey = $"game.created:{game.Id}",
                Payload = JsonSerializer.Serialize(new { GameId = game.Id, game.Name, DeviceType = game.SupportedDeviceType.ToString() })
            });
        }, cancellationToken);

        return Result<GameResult>.Success(new GameResult(game.Id, game.Name, game.IsActive));
    }
}

public class UpdateGameHandler
{
    private readonly IGameRepository _games;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGameHandler(IGameRepository games, IAuditLogRepository auditLogs, IOutboxRepository outbox, IUnitOfWork unitOfWork)
    {
        _games = games;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GameResult>> HandleAsync(UpdateGameCommand command, CancellationToken cancellationToken = default)
    {
        var game = await _games.GetByIdAsync(command.GameId);
        if (game is null)
        {
            return Result<GameResult>.Failure("بازی یافت نشد.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<GameResult>.Failure("نام بازی الزامی است.");
        }

        var before = JsonSerializer.Serialize(new { game.Name, game.Version, game.IsActive, game.SupportedDeviceType });

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            game.Name = command.Name.Trim();
            game.Version = command.Version;
            game.ExecutablePath = command.ExecutablePath;
            game.IconPath = command.IconPath;
            game.SupportedDeviceType = command.SupportedDeviceType;
            game.LaunchConfiguration = command.LaunchConfiguration;
            game.IsActive = command.IsActive;
            await _games.UpdateAsync(game);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "UpdateGame",
                EntityType = nameof(Game),
                EntityId = game.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                BeforeState = before,
                AfterState = JsonSerializer.Serialize(new { game.Name, game.Version, game.IsActive, game.SupportedDeviceType })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "game.updated",
                IdempotencyKey = $"game.updated:{game.Id}:{DateTime.UtcNow:O}",
                Payload = JsonSerializer.Serialize(new { GameId = game.Id, game.Name, game.IsActive })
            });
        }, cancellationToken);

        return Result<GameResult>.Success(new GameResult(game.Id, game.Name, game.IsActive));
    }
}

public class DeleteGameHandler
{
    private readonly IGameRepository _games;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGameHandler(IGameRepository games, IAuditLogRepository auditLogs, IOutboxRepository outbox, IUnitOfWork unitOfWork)
    {
        _games = games;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GameResult>> HandleAsync(DeleteGameCommand command, CancellationToken cancellationToken = default)
    {
        var game = await _games.GetByIdAsync(command.GameId);
        if (game is null)
        {
            return Result<GameResult>.Failure("بازی یافت نشد.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _games.DeleteAsync(game.Id);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "DeleteGame",
                EntityType = nameof(Game),
                EntityId = game.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                BeforeState = JsonSerializer.Serialize(new { game.Name, game.IsActive })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "game.deleted",
                IdempotencyKey = $"game.deleted:{game.Id}",
                Payload = JsonSerializer.Serialize(new { GameId = game.Id, game.Name })
            });
        }, cancellationToken);

        return Result<GameResult>.Success(new GameResult(game.Id, game.Name, false));
    }
}
