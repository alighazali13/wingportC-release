using GamePort.Cashier.Contracts.Responses;

namespace GamePort.Cashier.Application.Common;

/// <summary>
/// Pure presentation logic for Settings → Network &amp; Devices.
/// No UI framework dependencies — safe for unit tests.
/// </summary>
public static class NetworkDevicesMapper
{
    private static readonly char[] PersianDigits =
    {
        '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'
    };

    public const string TypeFilterAll = "";
    public const string TypeFilterPc = "PC";
    public const string TypeFilterPs5 = "PS5";

    public const string StatusFilterAll = "";

    public static string MapTypeDisplay(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "—";
        }

        return type.Trim() switch
        {
            var t when t.Equals("GamingPC", StringComparison.OrdinalIgnoreCase) => "PC",
            var t when t.Equals("PlayStation", StringComparison.OrdinalIgnoreCase) => "PS5",
            _ => type
        };
    }

    public static string MapStatusDisplay(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "—";
        }

        return status.Trim() switch
        {
            var s when s.Equals("Available", StringComparison.OrdinalIgnoreCase) => "آماده",
            var s when s.Equals("InUse", StringComparison.OrdinalIgnoreCase) => "در حال استفاده",
            var s when s.Equals("Offline", StringComparison.OrdinalIgnoreCase) => "آفلاین",
            var s when s.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) => "قطع ارتباط",
            var s when s.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) => "تعمیرات",
            _ => status
        };
    }

    public static string MapConnectionDisplay(bool isConnected)
        => isConnected ? "متصل" : "قطع ارتباط";

    public static string FormatHeartbeat(DateTime? lastHeartbeatUtc, DateTime? utcNow = null)
    {
        if (lastHeartbeatUtc is null)
        {
            return "—";
        }

        var now = utcNow ?? DateTime.UtcNow;
        var heartbeat = lastHeartbeatUtc.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(lastHeartbeatUtc.Value, DateTimeKind.Utc)
            : lastHeartbeatUtc.Value.ToUniversalTime();

        var delta = now - heartbeat;
        if (delta < TimeSpan.Zero)
        {
            heartbeat = now;
            delta = TimeSpan.Zero;
        }

        if (delta.TotalSeconds < 10)
        {
            return "همین الان";
        }

        if (delta.TotalSeconds < 60)
        {
            return "چند ثانیه پیش";
        }

        if (delta.TotalMinutes < 60)
        {
            var minutes = (int)Math.Floor(delta.TotalMinutes);
            return $"{ToPersianDigits(minutes)} دقیقه پیش";
        }

        if (delta.TotalHours < 24)
        {
            var hours = (int)Math.Floor(delta.TotalHours);
            return $"{ToPersianDigits(hours)} ساعت پیش";
        }

        var days = (int)Math.Floor(delta.TotalDays);
        return $"{ToPersianDigits(days)} روز پیش";
    }

    public static NetworkDevicesSummary BuildSummary(IReadOnlyList<DeviceResponse> devices)
    {
        var list = devices ?? Array.Empty<DeviceResponse>();
        return new NetworkDevicesSummary(
            TotalDevices: list.Count,
            PcCount: list.Count(d => IsPc(d)),
            Ps5Count: list.Count(d => IsPs5(d)),
            ConnectedCount: list.Count(d => d.IsConnected),
            DisconnectedCount: list.Count(d => !d.IsConnected),
            MaintenanceCount: list.Count(d => IsStatus(d, "Maintenance")));
    }

    public static IReadOnlyList<DeviceResponse> ApplyFilters(
        IReadOnlyList<DeviceResponse> devices,
        string? search,
        string? typeFilter,
        string? statusFilter)
    {
        var list = devices ?? Array.Empty<DeviceResponse>();

        IEnumerable<DeviceResponse> query = list;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => MatchesSearch(d, term));
        }

        var type = typeFilter?.Trim() ?? string.Empty;
        if (type.Equals(TypeFilterPc, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(IsPc);
        }
        else if (type.Equals(TypeFilterPs5, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(IsPs5);
        }

        var status = statusFilter?.Trim() ?? string.Empty;
        if (!string.Equals(status, StatusFilterAll, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => IsStatus(d, status));
        }

        return query.ToList();
    }

    public static bool MatchesSearch(DeviceResponse device, string search)
    {
        if (device is null || string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();

        return Contains(device.Name, term)
               || Contains(device.IpAddress, term)
               || Contains(device.MacAddress, term)
               || Contains(device.ClientIdentity, term);
    }

    public static bool IsPc(DeviceResponse device)
        => device is not null && device.Type.Equals("GamingPC", StringComparison.OrdinalIgnoreCase);

    public static bool IsPs5(DeviceResponse device)
        => device is not null && device.Type.Equals("PlayStation", StringComparison.OrdinalIgnoreCase);

    public static bool IsStatus(DeviceResponse device, string status)
        => device is not null && device.Status.Equals(status, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string term)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string ToPersianDigits(int value)
    {
        var chars = value.ToString().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = PersianDigits[chars[i] - '0'];
            }
        }

        return new string(chars);
    }
}

public sealed record NetworkDevicesSummary(
    int TotalDevices,
    int PcCount,
    int Ps5Count,
    int ConnectedCount,
    int DisconnectedCount,
    int MaintenanceCount);
