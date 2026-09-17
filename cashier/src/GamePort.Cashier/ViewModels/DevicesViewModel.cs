using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamePort.Cashier.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private List<DeviceItem> _allDevices = new();

    public ObservableCollection<StatsCard> StatsCards { get; } = new();
    public ObservableCollection<DeviceItem> Devices { get; } = new();

    [ObservableProperty]
    private bool _isGridView = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _typeFilter = "همه";

    [ObservableProperty]
    private string _statusFilter = "همه وضعیت‌ها";

    public DevicesViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

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

            var sessions = (sessionsResult.Data ?? new List<SessionResponse>())
                .GroupBy(s => s.DeviceId)
                .ToDictionary(g => g.Key, g => g.First());

            var customerNames = (customersResult.Data ?? new List<CustomerResponse>())
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            _allDevices = (devicesResult.Data ?? new List<DeviceResponse>())
                .Select(d => MapDevice(d, sessions, customerNames))
                .OrderBy(d => d.Name)
                .ToList();

            BuildStats(devicesResult.Data ?? new List<DeviceResponse>());
            ApplyFilters();
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
    private void SetGridView() => IsGridView = true;

    [RelayCommand]
    private void SetListView() => IsGridView = false;

    [RelayCommand]
    private void SetTypeFilter(string filter)
    {
        TypeFilter = filter;
        ApplyFilters();
    }

    [RelayCommand]
    private void SetStatusFilter(string filter)
    {
        StatusFilter = filter;
        ApplyFilters();
    }

    [RelayCommand]
    private void AddDevice()
    {
    }

    [RelayCommand]
    private async Task LockDevice(DeviceItem? device)
    {
        if (device is null)
        {
            return;
        }

        await _apiClient.SendDeviceCommandAsync(device.Id, "Lock");
    }

    [RelayCommand]
    private async Task UnlockDevice(DeviceItem? device)
    {
        if (device is null)
        {
            return;
        }

        await _apiClient.SendDeviceCommandAsync(device.Id, "Unlock");
    }

    [RelayCommand]
    private async Task RestartDevice(DeviceItem? device)
    {
        if (device is null)
        {
            return;
        }

        await _apiClient.SendDeviceCommandAsync(device.Id, "Restart");
    }

    [RelayCommand]
    private async Task ShutdownDevice(DeviceItem? device)
    {
        if (device is null)
        {
            return;
        }

        await _apiClient.SendDeviceCommandAsync(device.Id, "Shutdown");
    }

    private void ApplyFilters()
    {
        var query = _allDevices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(d =>
                d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.User.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(TypeFilter, "همه", StringComparison.OrdinalIgnoreCase))
        {
            var kind = TypeFilter.Contains("کنسول") || TypeFilter.Contains("PS", StringComparison.OrdinalIgnoreCase) ? "PS" : "PC";
            query = query.Where(d => d.Kind == kind);
        }

        Devices.Clear();
        foreach (var device in query)
        {
            Devices.Add(device);
        }
    }

    private void BuildStats(List<DeviceResponse> devices)
    {
        StatsCards.Clear();
        StatsCards.Add(new StatsCard { Label = "کل دستگاه‌ها", Value = PersianFormat.Number(devices.Count), ValueColor = "#181C23", AccentColor = "Transparent" });
        StatsCards.Add(new StatsCard { Label = "در حال استفاده", Value = PersianFormat.Number(devices.Count(d => d.Status == "InUse")), ValueColor = "#0058BC", AccentColor = "#0058BC" });
        StatsCards.Add(new StatsCard { Label = "آماده", Value = PersianFormat.Number(devices.Count(d => d.Status == "Available")), ValueColor = "#34C759", AccentColor = "#34C759" });
        StatsCards.Add(new StatsCard { Label = "قطع ارتباط", Value = PersianFormat.Number(devices.Count(d => d.Status == "Disconnected")), ValueColor = "#FF3B30", AccentColor = "#FF3B30" });
        StatsCards.Add(new StatsCard { Label = "خاموش", Value = PersianFormat.Number(devices.Count(d => d.Status == "Offline")), ValueColor = "#FF9500", AccentColor = "#FF9500" });
        StatsCards.Add(new StatsCard { Label = "تعمیرات", Value = PersianFormat.Number(devices.Count(d => d.Status == "Maintenance")), ValueColor = "#717786", AccentColor = "#717786" });
    }

    private static DeviceItem MapDevice(
        DeviceResponse device,
        Dictionary<Guid, SessionResponse> sessions,
        Dictionary<Guid, string> customerNames)
    {
        var isConsole = device.Type.Equals("PlayStation", StringComparison.OrdinalIgnoreCase);
        var status = device.Status switch
        {
            "InUse" => DeviceItemStatus.InUse,
            "Maintenance" => DeviceItemStatus.Maintenance,
            "Available" => DeviceItemStatus.Available,
            _ => DeviceItemStatus.Offline
        };

        var item = new DeviceItem
        {
            Id = device.Id,
            Name = device.Name,
            Kind = isConsole ? "PS" : "PC",
            Status = status,
            StatusText = device.Status switch
            {
                "InUse" => "در حال استفاده",
                "Maintenance" => "تعمیرات",
                "Available" => "آماده",
                "Disconnected" => "قطع ارتباط",
                "Offline" => "خاموش",
                _ => "-"
            },
            Icon = isConsole ? "\uEA28" : "\uE30A",
            TypeLabel = isConsole ? "PlayStation" : "Gaming PC",
            Specs = device.MacAddress is null ? string.Empty : $"MAC: {device.MacAddress}",
            LastConnection = device.LastHeartbeat.HasValue
                ? PersianFormat.Digits(device.LastHeartbeat.Value.ToLocalTime().ToString("HH:mm"))
                : "-"
        };

        if (sessions.TryGetValue(device.Id, out var session))
        {
            item.User = customerNames.TryGetValue(session.CustomerId, out var name) ? name : "مشتری";
            item.Time = PersianFormat.Digits(FormatElapsed(session.StartTime));
            item.Cost = PersianFormat.Number(Math.Round(session.PriceAtStart * (decimal)(DateTime.UtcNow - session.StartTime).TotalHours, 0)) + " تومان";
        }
        else if (status == DeviceItemStatus.Offline)
        {
            item.LockReason = "قطع ارتباط";
        }

        return item;
    }

    private static string FormatElapsed(DateTime startTime)
    {
        var elapsed = DateTime.UtcNow - startTime;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}

public enum DeviceItemStatus
{
    Available,
    InUse,
    Locked,
    Offline,
    Maintenance
}

public class DeviceItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public DeviceItemStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Specs { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Cost { get; set; } = string.Empty;
    public string LockReason { get; set; } = string.Empty;
    public string LastConnection { get; set; } = string.Empty;
}
