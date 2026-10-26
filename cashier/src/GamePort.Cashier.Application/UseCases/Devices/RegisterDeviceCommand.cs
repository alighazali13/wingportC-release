using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Devices;

public record RegisterDeviceCommand(
    string Name,
    DeviceType Type,
    string? IpAddress,
    string? MacAddress,
    string? HardwareInfo,
    Guid? ZoneId = null,
    Guid? PricingGroupId = null,
    int DisplayOrder = 0,
    string? ConsoleModel = null,
    int? ControllerCount = null,
    int? MaxPlayers = null,
    Guid? ActorEmployeeId = null);

public record RegisterDeviceResult(
    Device Device,
    string ClientSecret);
