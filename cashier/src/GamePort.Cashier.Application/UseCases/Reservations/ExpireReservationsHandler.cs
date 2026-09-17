using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Reservations;

public record ExpireReservationsResult(int ExpiredCount);

public class ExpireReservationsHandler
{
    private readonly IReservationRepository _reservations;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public ExpireReservationsHandler(
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

    public async Task<ExpireReservationsResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = (await _reservations.GetExpiredAsync(now)).ToList();
        if (expired.Count == 0)
        {
            return new ExpireReservationsResult(0);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var reservation in expired)
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
                    Payload = JsonSerializer.Serialize(new
                    {
                        ReservationId = reservation.Id,
                        reservation.CustomerId,
                        reservation.DeviceId,
                        reservation.EndTime
                    })
                });
            }
        }, cancellationToken);

        return new ExpireReservationsResult(expired.Count);
    }
}
