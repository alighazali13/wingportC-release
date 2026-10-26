using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Common;

/// <summary>
/// Eligibility guard for starting new sessions or creating new reservations.
/// Validates activation, operational state and the full Zone ancestor chain
/// (inactive or deleted zones at any level block new usage).
/// </summary>
public static class DeviceEligibilityGuard
{
    private const int MaxZoneDepth = 16;

    /// <param name="requireNotInUse">
    /// True for session start (InUse blocks a new session).
    /// False for reservations (a currently busy device may still be reserved for later).
    /// </param>
    public static async Task<string?> CheckNewUsageAsync(
        Device device,
        IZoneRepository zones,
        bool requireNotInUse,
        CancellationToken cancellationToken = default)
    {
        if (device.IsDeleted)
        {
            return "دستگاه حذف شده است.";
        }

        if (!device.IsActive)
        {
            return "دستگاه غیرفعال است.";
        }

        if (device.OperationalStatus == OperationalStatus.Maintenance)
        {
            return "دستگاه در وضعیت تعمیرات است.";
        }

        if (requireNotInUse
            && (device.OperationalStatus == OperationalStatus.InUse || device.Status == DeviceStatus.InUse))
        {
            return "دستگاه در حال حاضر در حال استفاده است.";
        }

        if (device.ZoneId.HasValue)
        {
            var zoneError = await CheckZoneChainAsync(zones, device.ZoneId.Value, cancellationToken);
            if (zoneError is not null)
            {
                return zoneError;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks the Zone ancestor chain (bounded to avoid cycles). GetByIdAsync already
    /// excludes soft-deleted zones, so a deleted zone at any level is reported as inactive.
    /// </summary>
    public static async Task<string?> CheckZoneChainAsync(
        IZoneRepository zones,
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        var zone = await zones.GetByIdAsync(zoneId);
        if (zone is null || !zone.IsActive)
        {
            return "منطقه دستگاه غیرفعال یا حذف شده است.";
        }

        var depth = 0;
        while (zone.ParentId.HasValue && depth < MaxZoneDepth)
        {
            var parent = await zones.GetByIdAsync(zone.ParentId.Value);
            if (parent is null || !parent.IsActive)
            {
                return "منطقه والد دستگاه غیرفعال یا حذف شده است.";
            }

            zone = parent;
            depth++;
        }

        return null;
    }
}
