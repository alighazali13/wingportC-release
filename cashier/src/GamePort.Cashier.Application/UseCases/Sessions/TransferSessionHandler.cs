using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Contracts.Hub;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class TransferSessionHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IDeviceRepository _devices;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IDeviceCommandSender _commandSender;
    private readonly IUnitOfWork _unitOfWork;

    public TransferSessionHandler(
        ISessionRepository sessions,
        IDeviceRepository devices,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IDeviceCommandSender commandSender,
        IUnitOfWork unitOfWork)
    {
        _sessions = sessions;
        _devices = devices;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _commandSender = commandSender;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TransferSessionResult>> HandleAsync(TransferSessionCommand command, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId);
        if (session is null)
        {
            return Result<TransferSessionResult>.Failure("نشست یافت نشد.");
        }

        if (session.Status is not (SessionStatus.Active or SessionStatus.Paused))
        {
            return Result<TransferSessionResult>.Failure("فقط نشست‌های باز قابل انتقال هستند.");
        }

        if (session.DeviceId == command.TargetDeviceId)
        {
            return Result<TransferSessionResult>.Failure("دستگاه مقصد با دستگاه فعلی یکسان است.");
        }

        var target = await _devices.GetByIdAsync(command.TargetDeviceId);
        if (target is null)
        {
            return Result<TransferSessionResult>.Failure("دستگاه مقصد یافت نشد.");
        }

        if (target.Status == DeviceStatus.InUse)
        {
            return Result<TransferSessionResult>.Failure("دستگاه مقصد در حال استفاده است.");
        }

        if (target.Type != session.DeviceType)
        {
            return Result<TransferSessionResult>.Failure("انتقال فقط بین دستگاه‌های هم‌نوع مجاز است.");
        }

        var source = await _devices.GetByIdAsync(session.DeviceId);
        var fromDeviceId = session.DeviceId;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (source is not null)
            {
                source.Status = DeviceStatus.Available;
                await _devices.UpdateAsync(source);
            }

            target.Status = DeviceStatus.InUse;
            await _devices.UpdateAsync(target);

            session.DeviceId = target.Id;
            session.DeviceType = target.Type;
            await _sessions.UpdateAsync(session);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "TransferSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = target.Id,
                BeforeState = JsonSerializer.Serialize(new { DeviceId = fromDeviceId }),
                AfterState = JsonSerializer.Serialize(new { DeviceId = target.Id })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.transferred",
                IdempotencyKey = $"session.transferred:{session.Id}:{target.Id}:{DateTime.UtcNow:O}",
                Payload = JsonSerializer.Serialize(new
                {
                    SessionId = session.Id,
                    session.CustomerId,
                    FromDeviceId = fromDeviceId,
                    ToDeviceId = target.Id
                })
            });
        }, cancellationToken);

        await NotifyClientsAsync(session, fromDeviceId, target.Id, cancellationToken);

        return Result<TransferSessionResult>.Success(
            new TransferSessionResult(session.Id, fromDeviceId, target.Id, session.Status.ToString()));
    }

    private async Task NotifyClientsAsync(Session session, Guid fromDeviceId, Guid toDeviceId, CancellationToken cancellationToken)
    {
        var restorePayload = JsonSerializer.Serialize(new SessionRestorePayload
        {
            SessionId = session.Id,
            CustomerId = session.CustomerId,
            CustomerName = session.Customer?.Name ?? string.Empty,
            DeviceId = toDeviceId,
            StartTime = session.StartTime,
            PlannedEndTime = session.PlannedEndTime,
            PriceAtStart = session.PriceAtStart,
            TotalPausedSeconds = session.TotalPausedSeconds,
            Status = session.Status.ToString()
        });

        await _commandSender.SendAsync(toDeviceId, new DeviceCommandMessage
        {
            DeviceId = toDeviceId,
            CommandType = DeviceCommandType.RestoreSession,
            Payload = restorePayload
        }, cancellationToken);

        await _commandSender.SendAsync(fromDeviceId, new DeviceCommandMessage
        {
            DeviceId = fromDeviceId,
            CommandType = DeviceCommandType.RefreshState
        }, cancellationToken);
    }
}
