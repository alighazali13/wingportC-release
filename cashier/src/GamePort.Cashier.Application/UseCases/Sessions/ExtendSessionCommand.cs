namespace GamePort.Cashier.Application.UseCases.Sessions;

public record ExtendSessionCommand(
    Guid SessionId,
    DateTime? NewPlannedEndTime,
    int? AdditionalMinutes,
    string? OperatorInfo);

public record ExtendSessionResult(
    Guid SessionId,
    DateTime? PlannedEndTime,
    string Status);
