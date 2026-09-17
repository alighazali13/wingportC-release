using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Devices;

public record RegisterDeviceCommand(
    string Name,
    DeviceType Type,
    string IpAddress,
    string? MacAddress,
    string? HardwareInfo);

public record RegisterDeviceResult(
    Device Device,
    string ClientSecret);
