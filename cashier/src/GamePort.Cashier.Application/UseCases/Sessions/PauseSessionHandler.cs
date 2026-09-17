using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Services;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class PauseSessionHandler
{
    private readonly ISessionRepository _sessions;
    private readonly SessionBillingService _billing;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public PauseSessionHandler(
        ISessionRepository sessions,
        SessionBillingService billing,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _sessions = sessions;
        _billing = billing;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PauseSessionResult>> HandleAsync(PauseSessionCommand command, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId);
        if (session is null)
        {
            return Result<PauseSessionResult>.Failure("نشست یافت نشد.");
        }

        if (session.Status != SessionStatus.Active)
        {
            return Result<PauseSessionResult>.Failure("فقط نشست فعال قابل توقف است.");
        }

        var pausedAt = DateTime.UtcNow;

        await _billing.ChargeUpToAsync(session, pausedAt, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.Status = SessionStatus.Paused;
            session.PausedAt = pausedAt;
            await _sessions.UpdateAsync(session);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "PauseSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = session.DeviceId,
                BeforeState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Active) }),
                AfterState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Paused), session.PausedAt, session.TotalCharged })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.paused",
                IdempotencyKey = $"session.paused:{session.Id}:{pausedAt:O}",
                Payload = JsonSerializer.Serialize(new { SessionId = session.Id, session.DeviceId, session.PausedAt })
            });
        }, cancellationToken);

        return Result<PauseSessionResult>.Success(
            new PauseSessionResult(session.Id, session.PausedAt ?? pausedAt, session.Status.ToString()));
    }
}
