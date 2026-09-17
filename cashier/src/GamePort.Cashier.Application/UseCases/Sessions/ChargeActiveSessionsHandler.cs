using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Services;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public record ChargeActiveSessionsResult(int ChargedCount, int InterruptedCount);

public class ChargeActiveSessionsHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IDeviceRepository _devices;
    private readonly SessionBillingService _billing;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public ChargeActiveSessionsHandler(
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

    public async Task<ChargeActiveSessionsResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = (await _sessions.GetActiveSessionsAsync()).ToList();

        var chargedCount = 0;
        var interruptedCount = 0;

        foreach (var session in sessions)
        {
            if (session.PausedAt is not null)
            {
                continue;
            }

            var outcome = await _billing.ChargeUpToAsync(session, now, cancellationToken);

            if (outcome.AmountCharged > 0)
            {
                chargedCount++;
            }

            if (outcome.InsufficientFunds)
            {
                await InterruptAsync(session, now, cancellationToken);
                interruptedCount++;
            }
        }

        return new ChargeActiveSessionsResult(chargedCount, interruptedCount);
    }

    private async Task InterruptAsync(Session session, DateTime endedAt, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.Status = SessionStatus.Interrupted;
            session.ActualEndTime = endedAt;
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
                Action = "InterruptSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.System,
                ActorName = "System",
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = session.DeviceId,
                Reason = "Insufficient wallet balance",
                AfterState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Interrupted), session.ActualEndTime })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.interrupted",
                IdempotencyKey = $"session.interrupted:{session.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    SessionId = session.Id,
                    session.CustomerId,
                    session.DeviceId,
                    session.ActualEndTime,
                    Reason = "InsufficientFunds"
                })
            });
        }, cancellationToken);
    }
}
