namespace GamePort.Cashier.Application.UseCases.Reservations;

public record CancelReservationCommand(
    Guid ReservationId,
    string? Reason,
    string? OperatorInfo);

public record CancelReservationResult(
    Guid ReservationId,
    string Status);
