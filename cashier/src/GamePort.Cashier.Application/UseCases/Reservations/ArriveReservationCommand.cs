namespace GamePort.Cashier.Application.UseCases.Reservations;

public record ArriveReservationCommand(
    Guid ReservationId,
    string? OperatorInfo);

public record ArriveReservationResult(
    Guid ReservationId,
    Guid SessionId,
    DateTime StartTime,
    DateTime? PlannedEndTime,
    bool WasEarly);
