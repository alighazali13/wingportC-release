using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Common;

/// <summary>
/// Validates optional Zone / PricingGroup references for device registration and updates.
/// Rules: null references are allowed; referenced entities must exist, be active and not deleted;
/// PricingGroup.DeviceType when set must match the device type.
/// </summary>
public static class DeviceReferenceValidator
{
    public static async Task<string?> ValidateZoneAsync(
        IZoneRepository zones,
        Guid? zoneId,
        CancellationToken cancellationToken = default)
    {
        if (zoneId is null)
        {
            return null;
        }

        var zone = await zones.GetByIdAsync(zoneId.Value);
        if (zone is null)
        {
            return "منطقه موردنظر یافت نشد.";
        }

        if (!zone.IsActive)
        {
            return "منطقه موردنظر غیرفعال است.";
        }

        return null;
    }

    public static async Task<string?> ValidatePricingGroupAsync(
        IPricingGroupRepository pricingGroups,
        Guid? pricingGroupId,
        DeviceType deviceType,
        CancellationToken cancellationToken = default)
    {
        if (pricingGroupId is null)
        {
            return null;
        }

        var group = await pricingGroups.GetByIdAsync(pricingGroupId.Value);
        if (group is null)
        {
            return "گروه قیمت‌گذاری موردنظر یافت نشد.";
        }

        if (!group.IsActive)
        {
            return "گروه قیمت‌گذاری موردنظر غیرفعال است.";
        }

        if (group.DeviceType.HasValue && group.DeviceType.Value != deviceType)
        {
            return "گروه قیمت‌گذاری موردنظر با نوع دستگاه سازگار نیست.";
        }

        return null;
    }
}
