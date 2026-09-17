using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class ResumeSessionHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public ResumeSessionHandler(
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

    public async Task<Result<ResumeSessionResult>> HandleAsync(ResumeSessionCommand command, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId);
        if (session is null)
        {
            return Result<ResumeSessionResult>.Failure("نشست یافت نشد.");
        }

        if (session.Status != SessionStatus.Paused)
        {
            return Result<ResumeSessionResult>.Failure("فقط نشست متوقف‌شده قابل ادامه است.");
        }

        var now = DateTime.UtcNow;
        var pausedSeconds = session.PausedAt.HasValue
            ? (long)Math.Max(0, (now - session.PausedAt.Value).TotalSeconds)
            : 0;

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.TotalPausedSeconds += pausedSeconds;
            session.PausedAt = null;
            session.Status = SessionStatus.Active;
            session.LastChargedAt = now;
            await _sessions.UpdateAsync(session);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "ResumeSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = session.DeviceId,
                BeforeState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Paused) }),
                AfterState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Active), session.TotalPausedSeconds })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.resumed",
                IdempotencyKey = $"session.resumed:{session.Id}:{now:O}",
                Payload = JsonSerializer.Serialize(new { SessionId = session.Id, session.DeviceId, session.TotalPausedSeconds })
            });
        }, cancellationToken);

        return Result<ResumeSessionResult>.Success(
            new ResumeSessionResult(session.Id, session.TotalPausedSeconds, session.Status.ToString()));
    }
}
