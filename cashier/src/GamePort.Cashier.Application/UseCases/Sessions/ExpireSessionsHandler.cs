using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Services;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public record ExpireSessionsResult(int ExpiredCount);

public class ExpireSessionsHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IDeviceRepository _devices;
    private readonly SessionBillingService _billing;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public ExpireSessionsHandler(
        ISessionRepository sessions,
        IDeviceRepository devices,
        SessionBillingService billing,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _sessions = sessions;
        _devices = devices;
        _billing = billing;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<ExpireSessionsResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = (await _sessions.GetExpiredAsync(now)).ToList();
        if (expired.Count == 0)
        {
            return new ExpireSessionsResult(0);
        }

        var expiredCount = 0;

        foreach (var session in expired)
        {
            var plannedEnd = session.PlannedEndTime ?? now;
            await _billing.ChargeUpToAsync(session, plannedEnd, cancellationToken);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var session in expired)
            {
                var plannedEnd = session.PlannedEndTime ?? now;
                session.Status = SessionStatus.Expired;
                session.ActualEndTime = plannedEnd;
                session.PausedAt = null;
                await _sessions.UpdateAsync(session);

                var device = await _devices.GetByIdAsync(session.DeviceId);
                if (device is not null)
                {
                    device.Status = DeviceStatus.Available;
                    await _devices.UpdateAsync(device);
                }

                await _auditLogs.AddAsync(new AuditLog
                {
                    Action = "ExpireSession",
                    EntityType = nameof(Session),
                    EntityId = session.Id,
                    ActorType = AuditActorType.System,
                    ActorName = "System",
                    Source = "Cashier",
                    SessionId = session.Id,
                    DeviceId = session.DeviceId,
                    AfterState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Expired), session.ActualEndTime, session.TotalCharged })
                });

                await _outbox.AddAsync(new OutboxEvent
                {
                    EventType = "session.expired",
                    IdempotencyKey = $"session.expired:{session.Id}",
                    Payload = JsonSerializer.Serialize(new
                    {
                        SessionId = session.Id,
                        session.CustomerId,
                        session.DeviceId,
                        session.ActualEndTime,
                        session.TotalCharged
                    })
                });

                expiredCount++;
            }
        }, cancellationToken);

        return new ExpireSessionsResult(expiredCount);
    }
}
