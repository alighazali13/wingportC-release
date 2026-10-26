using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.UseCases.Devices;

public enum DeviceLifecycleError
{
    None,
    Validation,
    NotFound,
    StaleVersion,
    Conflict
}

/// <summary>
/// Result of a device lifecycle mutation (activation, operational status, deletion).
/// Conflict maps to HTTP 409 for state conflicts (e.g. deleting a device with an open session).
/// </summary>
public record DeviceLifecycleResult(
    Device? Device,
    string? Error,
    DeviceLifecycleError ErrorKind)
{
    public bool IsSuccess => ErrorKind == DeviceLifecycleError.None && Device is not null;

    public static DeviceLifecycleResult Success(Device device) => new(device, null, DeviceLifecycleError.None);
    public static DeviceLifecycleResult Validation(string error) => new(null, error, DeviceLifecycleError.Validation);
    public static DeviceLifecycleResult NotFound() => new(null, "دستگاه یافت نشد.", DeviceLifecycleError.NotFound);
    public static DeviceLifecycleResult Stale(string error) => new(null, error, DeviceLifecycleError.StaleVersion);
    public static DeviceLifecycleResult Conflict(string error) => new(null, error, DeviceLifecycleError.Conflict);
}

public static class DeviceConcurrencyGuard
{
    public const string StaleVersionMessage =
        "نسخه ارسال‌شده منسوخ شده است؛ دستگاه توسط کاربر دیگری تغییر کرده است. لطفاً دوباره بارگذاری کنید.";

    /// <summary>Parses the Xmin token; returns false for missing/invalid values.</summary>
    public static bool TryParseXmin(string? xmin, out uint value) => uint.TryParse(xmin, out value);
}
