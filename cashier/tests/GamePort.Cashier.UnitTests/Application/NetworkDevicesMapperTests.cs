using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Contracts.Responses;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

public class NetworkDevicesMapperTests
{
    private static DeviceResponse Device(
        string name = "PC-01",
        string type = "GamingPC",
        string status = "Available",
        bool isConnected = true,
        string ip = "192.168.1.101",
        string? mac = "AA:BB:CC:DD:EE:01",
        string? clientIdentity = "dev-abc",
        DateTime? heartbeat = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Status = status,
            IpAddress = ip,
            MacAddress = mac,
            ClientIdentity = clientIdentity,
            IsConnected = isConnected,
            LastHeartbeat = heartbeat
        };

    [Theory]
    [InlineData("GamingPC", "PC")]
    [InlineData("gamingpc", "PC")]
    [InlineData("PlayStation", "PS5")]
    [InlineData("playstation", "PS5")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("", "—")]
    [InlineData(null, "—")]
    public void MapTypeDisplay_MapsWireValues(string? wire, string expected)
    {
        Assert.Equal(expected, NetworkDevicesMapper.MapTypeDisplay(wire));
    }

    [Theory]
    [InlineData("Available", "آماده")]
    [InlineData("InUse", "در حال استفاده")]
    [InlineData("Offline", "آفلاین")]
    [InlineData("Disconnected", "قطع ارتباط")]
    [InlineData("Maintenance", "تعمیرات")]
    [InlineData("Reserved", "Reserved")]
    [InlineData("", "—")]
    public void MapStatusDisplay_MapsBackendStatuses(string wire, string expected)
    {
        Assert.Equal(expected, NetworkDevicesMapper.MapStatusDisplay(wire));
    }

    [Fact]
    public void MapConnectionDisplay_KeepsConnectionSeparateFromStatus()
    {
        Assert.Equal("متصل", NetworkDevicesMapper.MapConnectionDisplay(true));
        Assert.Equal("قطع ارتباط", NetworkDevicesMapper.MapConnectionDisplay(false));
    }

    [Fact]
    public void FormatHeartbeat_NullReturnsDash()
    {
        Assert.Equal("—", NetworkDevicesMapper.FormatHeartbeat(null));
    }

    [Fact]
    public void FormatHeartbeat_RecentTimestamps()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("همین الان", NetworkDevicesMapper.FormatHeartbeat(now.AddSeconds(-3), now));
        Assert.Equal("چند ثانیه پیش", NetworkDevicesMapper.FormatHeartbeat(now.AddSeconds(-30), now));
        Assert.Equal("۲ دقیقه پیش", NetworkDevicesMapper.FormatHeartbeat(now.AddMinutes(-2), now));
        Assert.Equal("۱۵ دقیقه پیش", NetworkDevicesMapper.FormatHeartbeat(now.AddMinutes(-15), now));
        Assert.Equal("۳ ساعت پیش", NetworkDevicesMapper.FormatHeartbeat(now.AddHours(-3), now));
    }

    [Fact]
    public void BuildSummary_CountsTypesConnectionAndMaintenance()
    {
        var devices = new List<DeviceResponse>
        {
            Device(type: "GamingPC", status: "Available", isConnected: true),
            Device(type: "GamingPC", status: "Disconnected", isConnected: false),
            Device(type: "PlayStation", status: "Maintenance", isConnected: false),
            Device(type: "PlayStation", status: "InUse", isConnected: true)
        };

        var summary = NetworkDevicesMapper.BuildSummary(devices);

        Assert.Equal(4, summary.TotalDevices);
        Assert.Equal(2, summary.PcCount);
        Assert.Equal(2, summary.Ps5Count);
        Assert.Equal(2, summary.ConnectedCount);
        Assert.Equal(2, summary.DisconnectedCount);
        Assert.Equal(1, summary.MaintenanceCount);
    }

    [Fact]
    public void ApplyFilters_SearchIsCaseInsensitiveAcrossFields()
    {
        var devices = new List<DeviceResponse>
        {
            Device(name: "PC-01", ip: "192.168.1.101", mac: "AA:BB:CC:DD:EE:01", clientIdentity: "dev-alpha"),
            Device(name: "PS5-02", type: "PlayStation", ip: "192.168.1.102", mac: "AA:BB:CC:DD:EE:02", clientIdentity: "dev-beta")
        };

        var byName = NetworkDevicesMapper.ApplyFilters(devices, "pc-01", "", "");
        var byIp = NetworkDevicesMapper.ApplyFilters(devices, "1.102", "", "");
        var byMac = NetworkDevicesMapper.ApplyFilters(devices, "ee:02", "", "");
        var byIdentity = NetworkDevicesMapper.ApplyFilters(devices, "ALPHA", "", "");

        Assert.Single(byName);
        Assert.Single(byIp);
        Assert.Single(byMac);
        Assert.Single(byIdentity);
    }

    [Fact]
    public void ApplyFilters_TypeFilterPcAndPs5()
    {
        var devices = new List<DeviceResponse>
        {
            Device(type: "GamingPC"),
            Device(type: "GamingPC"),
            Device(type: "PlayStation")
        };

        Assert.Equal(2, NetworkDevicesMapper.ApplyFilters(devices, "", "PC", "").Count);
        Assert.Single(NetworkDevicesMapper.ApplyFilters(devices, "", "PS5", ""));
        Assert.Equal(3, NetworkDevicesMapper.ApplyFilters(devices, "", "", "").Count);
    }

    [Fact]
    public void ApplyFilters_StatusFilterUsesBackendWireValues()
    {
        var devices = new List<DeviceResponse>
        {
            Device(status: "Available"),
            Device(status: "InUse"),
            Device(status: "Offline"),
            Device(status: "Disconnected"),
            Device(status: "Maintenance"),
            Device(status: "Available", isConnected: false)
        };

        Assert.Equal(2, NetworkDevicesMapper.ApplyFilters(devices, "", "", "Available").Count);
        Assert.Single(NetworkDevicesMapper.ApplyFilters(devices, "", "", "InUse"));
        Assert.Single(NetworkDevicesMapper.ApplyFilters(devices, "", "", "Offline"));
        Assert.Single(NetworkDevicesMapper.ApplyFilters(devices, "", "", "Disconnected"));
        Assert.Single(NetworkDevicesMapper.ApplyFilters(devices, "", "", "Maintenance"));
    }

    [Fact]
    public void ApplyFilters_DoesNotConfuseStatusWithIsConnected()
    {
        var availableButOfflineConnection = Device(status: "Available", isConnected: false);

        var statusReady = NetworkDevicesMapper.ApplyFilters(new[] { availableButOfflineConnection }, "", "", "Available");
        var statusDisconnected = NetworkDevicesMapper.ApplyFilters(new[] { availableButOfflineConnection }, "", "", "Disconnected");

        Assert.Single(statusReady);
        Assert.Empty(statusDisconnected);
        Assert.Equal("قطع ارتباط", NetworkDevicesMapper.MapConnectionDisplay(availableButOfflineConnection.IsConnected));
        Assert.Equal("آماده", NetworkDevicesMapper.MapStatusDisplay(availableButOfflineConnection.Status));
    }

    [Fact]
    public void ApplyFilters_EmptySearchAndFiltersReturnsAll()
    {
        var devices = new List<DeviceResponse> { Device(), Device(type: "PlayStation") };

        Assert.Equal(2, NetworkDevicesMapper.ApplyFilters(devices, "  ", null, null).Count);
    }
}
