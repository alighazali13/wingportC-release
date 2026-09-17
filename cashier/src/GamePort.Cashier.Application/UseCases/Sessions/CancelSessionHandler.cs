using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Services;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class CancelSessionHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IDeviceRepository _devices;
    private readonly SessionBillingService _billing;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSessionHandler(
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

    public async Task<Result<CancelSessionResult>> HandleAsync(CancelSessionCommand command, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId);
        if (session is null)
        {
            return Result<CancelSessionResult>.Failure("نشست یافت نشد.");
        }

        if (session.Status is SessionStatus.Completed or SessionStatus.Cancelled or SessionStatus.Expired)
        {
            return Result<CancelSessionResult>.Failure("این نشست قبلاً بسته شده است.");
        }

        var cancelledAt = session.PausedAt ?? DateTime.UtcNow;
        var previousStatus = session.Status;

        await _billing.ChargeUpToAsync(session, cancelledAt, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.Status = SessionStatus.Cancelled;
            session.ActualEndTime = cancelledAt;
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
                Action = "CancelSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = session.DeviceId,
                Reason = command.Reason,
                BeforeState = JsonSerializer.Serialize(new { Status = previousStatus.ToString() }),
                AfterState = JsonSerializer.Serialize(new { Status = nameof(SessionStatus.Cancelled), session.ActualEndTime, session.TotalCharged })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.cancelled",
                IdempotencyKey = $"session.cancelled:{session.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    SessionId = session.Id,
                    session.CustomerId,
                    session.DeviceId,
                    session.ActualEndTime,
                    session.TotalCharged,
                    command.Reason
                })
            });
        }, cancellationToken);

        return Result<CancelSessionResult>.Success(
            new CancelSessionResult(session.Id, session.ActualEndTime ?? cancelledAt, session.Status.ToString()));
    }
}
