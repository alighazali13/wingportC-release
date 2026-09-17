namespace GamePort.Cashier.Application.UseCases.Sessions;

public record EndSessionCommand(
    Guid SessionId,
    string? Reason,
    string? OperatorInfo);

public record EndSessionResult(
    Guid SessionId,
    DateTime ActualEndTime,
    string Status);
