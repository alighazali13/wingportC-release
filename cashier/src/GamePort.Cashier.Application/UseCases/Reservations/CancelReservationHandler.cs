using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Reservations;

public class CancelReservationHandler
{
    private readonly IReservationRepository _reservations;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public CancelReservationHandler(
        IReservationRepository reservations,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _reservations = reservations;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CancelReservationResult>> HandleAsync(CancelReservationCommand command, CancellationToken cancellationToken = default)
    {
        var reservation = await _reservations.GetByIdAsync(command.ReservationId);
        if (reservation is null)
        {
            return Result<CancelReservationResult>.Failure("رزرو یافت نشد.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return Result<CancelReservationResult>.Failure("این رزرو قابل لغو نیست.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            reservation.Status = ReservationStatus.Cancelled;
            await _reservations.UpdateAsync(reservation);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "CancelReservation",
                EntityType = nameof(Reservation),
                EntityId = reservation.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                DeviceId = reservation.DeviceId,
                Reason = command.Reason,
                BeforeState = JsonSerializer.Serialize(new { Status = nameof(ReservationStatus.Reserved) }),
                AfterState = JsonSerializer.Serialize(new { Status = nameof(ReservationStatus.Cancelled) })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "reservation.cancelled",
                IdempotencyKey = $"reservation.cancelled:{reservation.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    ReservationId = reservation.Id,
                    reservation.CustomerId,
                    reservation.DeviceId,
                    command.Reason
                })
            });
        }, cancellationToken);

        return Result<CancelReservationResult>.Success(
            new CancelReservationResult(reservation.Id, reservation.Status.ToString()));
    }
}
