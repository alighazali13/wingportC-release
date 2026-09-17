namespace GamePort.Cashier.Application.UseCases.Sessions;

public record CancelSessionCommand(
    Guid SessionId,
    string? Reason,
    string? OperatorInfo);

public record CancelSessionResult(
    Guid SessionId,
    DateTime ActualEndTime,
    string Status);
