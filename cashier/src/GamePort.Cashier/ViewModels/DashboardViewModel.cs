using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GamePort.Cashier.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private const int EndingSoonThresholdMinutes = 15;

    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigation;
    private DispatcherTimer? _tickTimer;
    private List<DeviceCard> _allCards = new();

    public ObservableCollection<DeviceCard> PcDevices { get; } = new();
    public ObservableCollection<DeviceCard> PsDevices { get; } = new();
    public ObservableCollection<DeviceCard> FilteredDevices { get; } = new();
    public ObservableCollection<RecentActivity> RecentActivities { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isPcSelected = true;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeFilter = "all";

    // Counters
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _readyCount;
    [ObservableProperty] private int _endingSoonCount;
    [ObservableProperty] private int _reservedCount;
    [ObservableProperty] private int _offlineCount;

    // Action center
    public ObservableCollection<ActionCenterItem> ActionCenterItems { get; } = new();

    public int PcCount => PcDevices.Count;
    public int PsCount => PsDevices.Count;

    public event Action? OpenDeviceRequested;
    public event Action? OpenReservationsRequested;
    public event Action? OpenCustomersRequested;

    public DashboardViewModel(IApiClient apiClient, INavigationService navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
        _ = LoadAsync();

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => RefreshTimers();
        _tickTimer.Start();
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
            var reservationsResult = await _apiClient.GetUpcomingReservationsAsync();

            var devices = devicesResult.Data ?? new List<DeviceResponse>();
            var sessions = sessionsResult.Data ?? new List<SessionResponse>();

            var customerNames = (customersResult.Data ?? new List<CustomerResponse>())
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var sessionByDevice = sessions
                .GroupBy(s => s.DeviceId)
                .ToDictionary(g => g.Key, g => g.First());

            _allCards = devices
                .Select(d => CreateDeviceCard(d, sessionByDevice, customerNames))
                .ToList();

            PcDevices.Clear();
            PsDevices.Clear();
            foreach (var card in _allCards)
            {
                if (card.Type == "PS") PsDevices.Add(card);
                else PcDevices.Add(card);
            }

            BuildCounters();
            BuildActionCenter(_allCards, reservationsResult.Data ?? new List<ReservationResponse>());

            RecentActivities.Clear();
            foreach (var item in auditResult.Data ?? new List<AuditLogItem>())
                RecentActivities.Add(MapActivity(item));

            ApplyFilterAndSearch();
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
    private void SelectPc()
    {
        IsPcSelected = true;
        ApplyFilterAndSearch();
    }

    [RelayCommand]
    private void SelectPs5()
    {
        IsPcSelected = false;
        ApplyFilterAndSearch();
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        ActiveFilter = filter;
        ApplyFilterAndSearch();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        ActiveFilter = "all";
        SearchText = string.Empty;
        ApplyFilterAndSearch();
    }

    [RelayCommand]
    private void Refresh() => _ = LoadAsync();

    [RelayCommand]
    private void OpenPinModal() => WeakReferenceMessenger.Default.Send(new OpenPinModalMessage());

    [RelayCommand]
    private void GoToReservations() => OpenReservationsRequested?.Invoke();

    [RelayCommand]
    private void GoToNewCustomer() => OpenCustomersRequested?.Invoke();

    [RelayCommand]
    private void OpenDevice(DeviceCard? card) => OpenDeviceRequested?.Invoke();

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSearch();

    private void ApplyFilterAndSearch()
    {
        var source = IsPcSelected ? PcDevices : PsDevices;

        IEnumerable<DeviceCard> filtered = source;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            filtered = filtered.Where(c =>
                c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.User.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Zone.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (ActiveFilter != "all")
        {
            filtered = ActiveFilter switch
            {
                "active" => filtered.Where(c => c.Status == DeviceState.InUse),
                "ready" => filtered.Where(c => c.Status == DeviceState.Available),
                "ending" => filtered.Where(c => c.IsEndingSoon),
                "reserved" => filtered.Where(c => c.Status == DeviceState.Reserved),
                "offline" => filtered.Where(c => c.Status == DeviceState.Offline || c.Status == DeviceState.Disconnected),
                _ => filtered
            };
        }

        FilteredDevices.Clear();
        foreach (var card in filtered)
            FilteredDevices.Add(card);
    }

    private void RefreshTimers()
    {
        foreach (var card in FilteredDevices)
        {
            if (card.Status == DeviceState.InUse && card.SessionStart.HasValue)
            {
                var elapsed = DateTime.UtcNow - card.SessionStart.Value;
                if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                card.Time = PersianFormat.Digits($"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}");

                if (card.PlannedEnd.HasValue)
                {
                    var remaining = card.PlannedEnd.Value - DateTime.UtcNow;
                    card.IsEndingSoon = remaining.TotalMinutes > 0 && remaining.TotalMinutes <= EndingSoonThresholdMinutes;
                    card.RemainingText = card.IsEndingSoon
                        ? PersianFormat.Digits($"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}")
                        : string.Empty;
                }
            }
        }
    }

    private void BuildCounters()
    {
        TotalCount = _allCards.Count;
        ActiveCount = _allCards.Count(c => c.Status == DeviceState.InUse);
        ReadyCount = _allCards.Count(c => c.Status == DeviceState.Available);
        OfflineCount = _allCards.Count(c => c.Status == DeviceState.Offline || c.Status == DeviceState.Disconnected);
        EndingSoonCount = _allCards.Count(c => c.IsEndingSoon);
        ReservedCount = _allCards.Count(c => c.Status == DeviceState.Reserved);

        OnPropertyChanged(nameof(PcCount));
        OnPropertyChanged(nameof(PsCount));
    }

    private void BuildActionCenter(List<DeviceCard> cards, List<ReservationResponse> reservations)
    {
        ActionCenterItems.Clear();

        var offline = cards.Where(c => c.Status == DeviceState.Offline || c.Status == DeviceState.Disconnected).ToList();
        if (offline.Count > 0)
            ActionCenterItems.Add(new ActionCenterItem
            {
                Type = "offline",
                Count = offline.Count,
                Description = $"{offline.Count} آفلاین ({offline[0].Name})",
                Color = "#EF4444"
            });

        var ending = cards.Where(c => c.IsEndingSoon).ToList();
        if (ending.Count > 0)
            ActionCenterItems.Add(new ActionCenterItem
            {
                Type = "ending",
                Count = ending.Count,
                Description = $"{ending.Count} نزدیک پایان ({ending[0].Name})",
                Color = "#F59E0B"
            });

        if (reservations.Count > 0)
            ActionCenterItems.Add(new ActionCenterItem
            {
                Type = "reserved",
                Count = reservations.Count,
                Description = $"{reservations.Count} رزرو نزدیک",
                Color = "#F59E0B"
            });
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
            "Reserved" => DeviceState.Reserved,
            "Disconnected" => DeviceState.Disconnected,
            _ => DeviceState.Offline
        };

        var card = new DeviceCard
        {
            Name = device.Name,
            Zone = device.IpAddress,
            Type = isConsole ? "PS" : "PC",
            Status = state,
            StatusText = device.Status switch
            {
                "InUse" => "در حال بازی",
                "Maintenance" => "تعمیرات",
                "Available" => "آماده",
                "Reserved" => "رزرو شده",
                "Disconnected" => "قطع ارتباط",
                "Offline" => "آفلاین",
                _ => "-"
            },
            User = "-",
            Icon = isConsole ? "\uEA28" : "\uE30A"
        };

        if (state == DeviceState.InUse && sessionByDevice.TryGetValue(device.Id, out var session))
        {
            card.User = customerNames.TryGetValue(session.CustomerId, out var name) ? name : "مشتری";
            card.SessionStart = session.StartTime;
            card.PlannedEnd = session.PlannedEndTime;

            var elapsed = DateTime.UtcNow - session.StartTime;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            card.Time = PersianFormat.Digits($"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}");

            if (session.PlannedEndTime.HasValue)
            {
                var remaining = session.PlannedEndTime.Value - DateTime.UtcNow;
                card.IsEndingSoon = remaining.TotalMinutes > 0 && remaining.TotalMinutes <= EndingSoonThresholdMinutes;
                card.RemainingText = card.IsEndingSoon
                    ? PersianFormat.Digits($"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}")
                    : string.Empty;
            }
        }
        else if (state == DeviceState.Offline)
        {
            card.User = device.IsConnected ? "قطع ارتباط" : "آفلاین";
        }

        return card;
    }

    private static RecentActivity MapActivity(AuditLogItem item)
    {
        var (title, icon, type) = item.Action switch
        {
            "StartSession" => ("آغاز نشست", "\uE768", "Info"),
            "EndSession" => ("پایان نشست", "\uE73E", "Success"),
            "CancelSession" => ("لغو نشست", "\uE711", "Warning"),
            "ExpireSession" => ("انقضای نشست", "\uE823", "Warning"),
            "InterruptSession" => ("قطع نشست", "\uE7BA", "Warning"),
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
    Reserved,
    Offline,
    Disconnected
}

public partial class DeviceCard : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Zone { get; set; } = "-";
    public string Type { get; set; } = string.Empty;
    public DeviceState Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public DateTime? SessionStart { get; set; }
    public DateTime? PlannedEnd { get; set; }

    [ObservableProperty] private bool _isEndingSoon;
    [ObservableProperty] private string _remainingText = string.Empty;
}

public class StatsCard
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueColor { get; set; } = "#181C23";
    public string AccentColor { get; set; } = "Transparent";
}

public class ActionCenterItem
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#EF4444";
}

public class RecentActivity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
}
