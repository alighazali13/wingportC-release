using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.UseCases.Devices;

public record UpdateDeviceCommand(
    Guid DeviceId,
    string Name,
    string? IpAddress,
    string? MacAddress,
    Guid? ZoneId,
    Guid? PricingGroupId,
    int DisplayOrder,
    string? MaintenanceNotes,
    string? ConsoleModel,
    int? ControllerCount,
    int? MaxPlayers,
    uint Xmin,
    Guid? ActorEmployeeId = null);

public enum UpdateDeviceError
{
    None,
    Validation,
    NotFound,
    StaleVersion
}

public record UpdateDeviceResult(
    Device? Device,
    string? Error,
    UpdateDeviceError ErrorKind)
{
    public bool IsSuccess => ErrorKind == UpdateDeviceError.None && Device is not null;

    public static UpdateDeviceResult Success(Device device) => new(device, null, UpdateDeviceError.None);
    public static UpdateDeviceResult Validation(string error) => new(null, error, UpdateDeviceError.Validation);
    public static UpdateDeviceResult NotFound() => new(null, "دستگاه یافت نشد.", UpdateDeviceError.NotFound);
    public static UpdateDeviceResult Stale(string error) => new(null, error, UpdateDeviceError.StaleVersion);
}
