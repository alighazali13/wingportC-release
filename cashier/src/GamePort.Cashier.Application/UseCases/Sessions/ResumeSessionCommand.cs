namespace GamePort.Cashier.Application.UseCases.Sessions;

public record ResumeSessionCommand(
    Guid SessionId,
    string? OperatorInfo);

public record ResumeSessionResult(
    Guid SessionId,
    long TotalPausedSeconds,
    string Status);
