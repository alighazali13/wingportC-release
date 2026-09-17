using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Games;

public record CreateGameCommand(
    string Name,
    string? Version,
    string? ExecutablePath,
    string? IconPath,
    DeviceType SupportedDeviceType,
    string? LaunchConfiguration,
    string? OperatorInfo);

public record GameResult(
    Guid GameId,
    string Name,
    bool IsActive);

public record UpdateGameCommand(
    Guid GameId,
    string Name,
    string? Version,
    string? ExecutablePath,
    string? IconPath,
    DeviceType SupportedDeviceType,
    string? LaunchConfiguration,
    bool IsActive,
    string? OperatorInfo);

public record DeleteGameCommand(
    Guid GameId,
    string? OperatorInfo);
