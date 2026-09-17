namespace GamePort.Cashier.Application.UseCases.Sessions;

public record PauseSessionCommand(
    Guid SessionId,
    string? OperatorInfo);

public record PauseSessionResult(
    Guid SessionId,
    DateTime PausedAt,
    string Status);
