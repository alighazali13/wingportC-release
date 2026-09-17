using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Services;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class EndSessionHandler
{
    private readonly ISessionRepository _sessions;
    private readonly IDeviceRepository _devices;
    private readonly SessionBillingService _billing;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public EndSessionHandler(
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

    public async Task<Result<EndSessionResult>> HandleAsync(EndSessionCommand command, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId);
        if (session is null)
        {
            return Result<EndSessionResult>.Failure("نشست یافت نشد.");
        }

        if (session.Status is SessionStatus.Completed or SessionStatus.Cancelled or SessionStatus.Expired)
        {
            return Result<EndSessionResult>.Failure("این نشست قبلاً پایان یافته است.");
        }

        var endTime = session.PausedAt ?? DateTime.UtcNow;
        var beforeStatus = session.Status;

        await _billing.ChargeUpToAsync(session, endTime, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.ActualEndTime = endTime;
            session.Status = SessionStatus.Completed;
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
                Action = "EndSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = session.DeviceId,
                Reason = command.Reason,
                BeforeState = JsonSerializer.Serialize(new { Status = beforeStatus.ToString() }),
                AfterState = JsonSerializer.Serialize(new { Status = session.Status.ToString(), session.ActualEndTime, session.TotalCharged })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.ended",
                IdempotencyKey = $"session.ended:{session.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    SessionId = session.Id,
                    session.CustomerId,
                    session.DeviceId,
                    session.ActualEndTime,
                    session.TotalCharged
                })
            });
        }, cancellationToken);

        return Result<EndSessionResult>.Success(
            new EndSessionResult(session.Id, session.ActualEndTime ?? endTime, session.Status.ToString()));
    }
}
