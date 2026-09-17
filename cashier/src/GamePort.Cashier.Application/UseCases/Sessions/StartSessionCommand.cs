namespace GamePort.Cashier.Application.UseCases.Sessions;

public record StartSessionCommand(
    Guid CustomerId,
    Guid DeviceId,
    DateTime? PlannedEndTime,
    string? OperatorInfo,
    Guid? ReservationId = null);

public record StartSessionResult(
    Guid SessionId,
    Guid CustomerId,
    Guid DeviceId,
    decimal PriceAtStart,
    DateTime StartTime,
    DateTime? PlannedEndTime);
