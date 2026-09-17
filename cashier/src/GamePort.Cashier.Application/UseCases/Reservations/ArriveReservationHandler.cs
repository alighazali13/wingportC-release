using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Reservations;

public class ArriveReservationHandler
{
    private readonly IReservationRepository _reservations;
    private readonly StartSessionHandler _startSession;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public ArriveReservationHandler(
        IReservationRepository reservations,
        StartSessionHandler startSession,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _reservations = reservations;
        _startSession = startSession;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ArriveReservationResult>> HandleAsync(ArriveReservationCommand command, CancellationToken cancellationToken = default)
    {
        var reservation = await _reservations.GetByIdAsync(command.ReservationId);
        if (reservation is null)
        {
            return Result<ArriveReservationResult>.Failure("رزرو یافت نشد.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return Result<ArriveReservationResult>.Failure("این رزرو دیگر قابل استفاده نیست.");
        }

        var now = DateTime.UtcNow;
        if (now >= reservation.EndTime)
        {
            await ExpireAsync(reservation, cancellationToken);
            return Result<ArriveReservationResult>.Failure("زمان رزرو به پایان رسیده است.");
        }

        var duration = reservation.EndTime - reservation.StartTime;
        var wasEarly = now < reservation.StartTime;

        // Early arrival: keep the reserved duration (start earlier, end earlier).
        // On-time/late arrival: keep the reserved end time (usable time is shortened).
        var plannedEnd = wasEarly ? now + duration : reservation.EndTime;

        var startResult = await _startSession.HandleAsync(
            new StartSessionCommand(
                reservation.CustomerId,
                reservation.DeviceId,
                plannedEnd,
                command.OperatorInfo,
                reservation.Id),
            cancellationToken);

        if (!startResult.IsSuccess)
        {
            return Result<ArriveReservationResult>.Failure(startResult.Error!);
        }

        return Result<ArriveReservationResult>.Success(new ArriveReservationResult(
            reservation.Id,
            startResult.Value!.SessionId,
            startResult.Value.StartTime,
            startResult.Value.PlannedEndTime,
            wasEarly));
    }

    private async Task ExpireAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            reservation.Status = ReservationStatus.Expired;
            await _reservations.UpdateAsync(reservation);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "ExpireReservation",
                EntityType = nameof(Reservation),
                EntityId = reservation.Id,
                ActorType = AuditActorType.System,
                ActorName = "System",
                Source = "Cashier",
                DeviceId = reservation.DeviceId,
                Reason = "Customer did not arrive",
                AfterState = JsonSerializer.Serialize(new { Status = nameof(ReservationStatus.Expired) })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "reservation.expired",
                IdempotencyKey = $"reservation.expired:{reservation.Id}",
                Payload = JsonSerializer.Serialize(new { ReservationId = reservation.Id, reservation.CustomerId, reservation.DeviceId })
            });
        }, cancellationToken);
    }
}
