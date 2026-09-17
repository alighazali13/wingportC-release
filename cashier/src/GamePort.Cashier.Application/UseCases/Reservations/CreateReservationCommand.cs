namespace GamePort.Cashier.Application.UseCases.Reservations;

public record CreateReservationCommand(
    Guid CustomerId,
    Guid DeviceId,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes,
    string? Source,
    string? OperatorInfo);

public record CreateReservationResult(
    Guid ReservationId,
    Guid CustomerId,
    Guid DeviceId,
    DateTime StartTime,
    DateTime EndTime,
    string Status);
