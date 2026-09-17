using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class ExtendSessionHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendSessionHandler(
        ISessionRepository sessions,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _sessions = sessions;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExtendSessionResult>> HandleAsync(ExtendSessionCommand command, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId);
        if (session is null)
        {
            return Result<ExtendSessionResult>.Failure("نشست یافت نشد.");
        }

        if (session.Status is not (SessionStatus.Active or SessionStatus.Paused))
        {
            return Result<ExtendSessionResult>.Failure("فقط نشست‌های باز قابل تمدید هستند.");
        }

        var now = DateTime.UtcNow;

        DateTime newPlannedEnd;
        if (command.NewPlannedEndTime.HasValue)
        {
            newPlannedEnd = command.NewPlannedEndTime.Value;
        }
        else if (command.AdditionalMinutes is > 0)
        {
            var baseTime = session.PlannedEndTime.HasValue && session.PlannedEndTime.Value > now
                ? session.PlannedEndTime.Value
                : now;
            newPlannedEnd = baseTime.AddMinutes(command.AdditionalMinutes.Value);
        }
        else
        {
            return Result<ExtendSessionResult>.Failure("زمان پایان جدید یا تعداد دقیقه برای تمدید الزامی است.");
        }

        if (newPlannedEnd <= session.StartTime || newPlannedEnd <= now)
        {
            return Result<ExtendSessionResult>.Failure("زمان پایان جدید باید در آینده و بعد از شروع نشست باشد.");
        }

        var previousPlannedEnd = session.PlannedEndTime;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.PlannedEndTime = newPlannedEnd;
            await _sessions.UpdateAsync(session);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "ExtendSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = session.DeviceId,
                BeforeState = JsonSerializer.Serialize(new { PlannedEndTime = previousPlannedEnd }),
                AfterState = JsonSerializer.Serialize(new { PlannedEndTime = session.PlannedEndTime })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.extended",
                IdempotencyKey = $"session.extended:{session.Id}:{session.PlannedEndTime:O}",
                Payload = JsonSerializer.Serialize(new
                {
                    SessionId = session.Id,
                    session.CustomerId,
                    session.DeviceId,
                    session.PlannedEndTime
                })
            });
        }, cancellationToken);

        return Result<ExtendSessionResult>.Success(
            new ExtendSessionResult(session.Id, session.PlannedEndTime, session.Status.ToString()));
    }
}
