namespace GamePort.Cashier.Contracts.Responses;

public class DashboardResponse
{
    public int TotalDevices { get; set; }
    public int TotalPCs { get; set; }
    public int TotalPS { get; set; }
    public int DevicesInUse { get; set; }
    public int DevicesAvailable { get; set; }
    public int DevicesOffline { get; set; }
    public int DevicesMaintenance { get; set; }
    public decimal TodayRevenue { get; set; }
    public int ActiveSessions { get; set; }
    public List<DeviceStatusDto> Devices { get; set; } = new();
    public List<RecentActivityDto> RecentActivities { get; set; } = new();
}

public class DeviceStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CurrentUser { get; set; }
    public bool IsConnected { get; set; }
}

public class RecentActivityDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Time { get; set; }
    public string ActivityType { get; set; } = string.Empty;
}
