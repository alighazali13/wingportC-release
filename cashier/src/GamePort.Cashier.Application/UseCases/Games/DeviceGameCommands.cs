namespace GamePort.Cashier.Application.UseCases.Games;

public record AssignGameToDeviceCommand(
    Guid DeviceId,
    Guid GameId,
    bool IsInstalled,
    string? InstalledVersion,
    string? OperatorInfo);

public record DeviceGameResult(
    Guid DeviceId,
    Guid GameId,
    string GameName,
    bool IsInstalled);

public record UnassignGameFromDeviceCommand(
    Guid DeviceId,
    Guid GameId,
    string? OperatorInfo);

public record LaunchGameCommand(
    Guid DeviceId,
    Guid GameId,
    string? OperatorInfo);

public record LaunchGameResult(
    Guid DeviceId,
    Guid GameId,
    Guid CommandId,
    bool Delivered);

public record CloseGameCommand(
    Guid DeviceId,
    string? OperatorInfo);

public record CloseGameResult(
    Guid DeviceId,
    Guid CommandId,
    bool Delivered);
