using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GamePort.Cashier.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;

    public ObservableCollection<StatsCard> StatsCards { get; } = new();
    public ObservableCollection<DeviceCard> PcDevices { get; } = new();
    public ObservableCollection<DeviceCard> PsDevices { get; } = new();
    public ObservableCollection<RecentActivity> RecentActivities { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public DashboardViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var devicesResult = await _apiClient.GetDevicesAsync();
            if (!devicesResult.IsSuccess)
            {
                ErrorMessage = devicesResult.Error;
                return;
            }

            var sessionsResult = await _apiClient.GetActiveSessionsAsync();
            var customersResult = await _apiClient.SearchCustomersAsync(null);
            var auditResult = await _apiClient.GetRecentAuditAsync(6);

            var devices = devicesResult.Data ?? new List<DeviceResponse>();
            var sessions = sessionsResult.Data ?? new List<SessionResponse>();

            var customerNames = (customersResult.Data ?? new List<CustomerResponse>())
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var sessionByDevice = sessions
                .GroupBy(s => s.DeviceId)
                .ToDictionary(g => g.Key, g => g.First());

            PcDevices.Clear();
            PsDevices.Clear();

            foreach (var device in devices)
            {
                var card = CreateDeviceCard(device, sessionByDevice, customerNames);
                if (device.Type.Equals("PlayStation", StringComparison.OrdinalIgnoreCase))
                {
                    PsDevices.Add(card);
                }
                else
                {
                    PcDevices.Add(card);
                }
            }

            BuildStats(devices);

            RecentActivities.Clear();
            foreach (var item in auditResult.Data ?? new List<AuditLogItem>())
            {
                RecentActivities.Add(MapActivity(item));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenPinModal()
    {
        WeakReferenceMessenger.Default.Send(new OpenPinModalMessage());
    }

    private void BuildStats(List<DeviceResponse> devices)
    {
        StatsCards.Clear();

        var inUse = devices.Count(d => d.Status == "InUse");
        var available = devices.Count(d => d.Status == "Available");
        var maintenance = devices.Count(d => d.Status == "Maintenance");

        StatsCards.Add(new StatsCard { Label = "کل دستگاه‌ها", Value = PersianFormat.Number(devices.Count), ValueColor = "#181C23", AccentColor = "Transparent" });
        StatsCards.Add(new StatsCard { Label = "کامپیوتر (PC)", Value = PersianFormat.Number(PcDevices.Count), ValueColor = "#181C23", AccentColor = "#0058BC" });
        StatsCards.Add(new StatsCard { Label = "کنسول (PS)", Value = PersianFormat.Number(PsDevices.Count), ValueColor = "#181C23", AccentColor = "#4C4ACA" });
        StatsCards.Add(new StatsCard { Label = "در حال استفاده", Value = PersianFormat.Number(inUse), ValueColor = "#0070EB", AccentColor = "#0070EB" });
        StatsCards.Add(new StatsCard { Label = "آماده", Value = PersianFormat.Number(available), ValueColor = "#34C759", AccentColor = "#34C759" });
        StatsCards.Add(new StatsCard { Label = "تعمیرات", Value = PersianFormat.Number(maintenance), ValueColor = "#FF9500", AccentColor = "#FF9500" });
    }

    private static DeviceCard CreateDeviceCard(
        DeviceResponse device,
        Dictionary<Guid, SessionResponse> sessionByDevice,
        Dictionary<Guid, string> customerNames)
    {
        var isConsole = device.Type.Equals("PlayStation", StringComparison.OrdinalIgnoreCase);
        var state = device.Status switch
        {
            "InUse" => DeviceState.InUse,
            "Maintenance" => DeviceState.Maintenance,
            "Available" => DeviceState.Available,
            _ => DeviceState.Offline
        };

        var card = new DeviceCard
        {
            Name = device.Name,
            Type = isConsole ? "PS" : "PC",
            Status = state,
            StatusText = device.Status switch
            {
                "InUse" => "در حال استفاده",
                "Maintenance" => "تعمیرات",
                "Available" => "آماده",
                "Disconnected" => "قطع ارتباط",
                "Offline" => "خاموش",
                _ => "-"
            },
            User = "-",
            Icon = isConsole ? "\uEA28" : "\uE30A"
        };

        if (state == DeviceState.InUse && sessionByDevice.TryGetValue(device.Id, out var session))
        {
            card.User = customerNames.TryGetValue(session.CustomerId, out var name) ? name : "مشتری";
            card.Time = FormatElapsed(session.StartTime);
            card.Cost = PersianFormat.Number(CalculateCost(session)) + " تومان";
        }
        else if (state == DeviceState.Offline)
        {
            card.User = device.IsConnected ? "قطع ارتباط" : "خاموش";
        }

        return card;
    }

    private static decimal CalculateCost(SessionResponse session)
    {
        var elapsedHours = (decimal)(DateTime.UtcNow - session.StartTime).TotalHours;
        if (elapsedHours < 0)
        {
            elapsedHours = 0;
        }

        return Math.Round(session.PriceAtStart * elapsedHours, 0);
    }

    private static string FormatElapsed(DateTime startTime)
    {
        var elapsed = DateTime.UtcNow - startTime;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return PersianFormat.Digits($"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}");
    }

    private static RecentActivity MapActivity(AuditLogItem item)
    {
        var (title, icon, type) = item.Action switch
        {
            "StartSession" => ("آغاز نشست", "\uE768", "Info"),
            "EndSession" => ("پایان نشست", "\uE73E", "Success"),
            "CancelSession" => ("لغو نشست", "\uE711", "Warning"),
            "ExpireSession" => ("انقضای نشست", "\uE823", "Warning"),
            "InterruptSession" => ("قطع نشست (کمبود موجودی)", "\uE7BA", "Warning"),
            "RegisterCustomer" => ("ثبت مشتری جدید", "\uE8D4", "Info"),
            "WalletRecharge" => ("شارژ کیف پول", "\uE8C7", "Success"),
            "RegisterDevice" => ("ثبت دستگاه", "\uE977", "Info"),
            "LaunchGame" => ("اجرای بازی", "\uE7FC", "Info"),
            "EmployeeLogin" => ("ورود کارمند", "\uE7FF", "Info"),
            _ => (item.Action, "\uE946", "Info")
        };

        return new RecentActivity
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(item.ActorName) ? item.Source : item.ActorName,
            Time = PersianFormat.Digits(item.CreatedAt.ToLocalTime().ToString("HH:mm")),
            Icon = icon,
            ActivityType = type
        };
    }
}

public enum DeviceState
{
    Available,
    InUse,
    Maintenance,
    Offline
}

public class StatsCard
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueColor { get; set; } = "#181C23";
    public string AccentColor { get; set; } = "Transparent";
}

public class DeviceCard
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DeviceState Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Cost { get; set; } = string.Empty;
}

public class RecentActivity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
}
