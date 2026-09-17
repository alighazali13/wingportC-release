namespace GamePort.Cashier.Application.UseCases.Sessions;

public record TransferSessionCommand(
    Guid SessionId,
    Guid TargetDeviceId,
    string? OperatorInfo);

public record TransferSessionResult(
    Guid SessionId,
    Guid FromDeviceId,
    Guid ToDeviceId,
    string Status);
