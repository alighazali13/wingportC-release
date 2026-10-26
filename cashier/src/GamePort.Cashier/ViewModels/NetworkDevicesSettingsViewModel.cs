using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

/// <summary>
/// Settings → Network &amp; Devices child ViewModel.
/// Inventory / registration / connectivity administration only.
/// </summary>
public partial class NetworkDevicesSettingsViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private List<DeviceResponse> _allDevices = new();
    private bool _loadedOnce;

    public ObservableCollection<NetworkDeviceRow> Devices { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedTypeFilter = NetworkDevicesMapper.TypeFilterAll;

    [ObservableProperty]
    private string _selectedStatusFilter = NetworkDevicesMapper.StatusFilterAll;

    [ObservableProperty]
    private bool _isDevicesLoading;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _showTable;

    [ObservableProperty]
    private string _totalDevicesText = "۰";

    [ObservableProperty]
    private string _pcCountText = "۰";

    [ObservableProperty]
    private string _ps5CountText = "۰";

    [ObservableProperty]
    private string _connectedCountText = "۰";

    [ObservableProperty]
    private string _disconnectedCountText = "۰";

    [ObservableProperty]
    private string _maintenanceCountText = "۰";

    [ObservableProperty]
    private string _visibleCountText = "۰";

    [ObservableProperty]
    private string _machineName = "—";

    [ObservableProperty]
    private string _appVersion = "—";

    [ObservableProperty]
    private string _databaseProvider = "—";

    [ObservableProperty]
    private string _serverUrlsText = "—";

    [ObservableProperty]
    private string _cloudConfiguredText = "—";

    [ObservableProperty]
    private string _lanInfoText = "اطلاعات در دسترس نیست";

    [ObservableProperty]
    private bool _isInfrastructureLoading;

    [ObservableProperty]
    private string? _addDeviceNotice;

    [ObservableProperty]
    private bool _isAddDeviceNoticeVisible;

    public bool IsTypeAll => string.Equals(SelectedTypeFilter, NetworkDevicesMapper.TypeFilterAll, StringComparison.OrdinalIgnoreCase);
    public bool IsTypePc => string.Equals(SelectedTypeFilter, NetworkDevicesMapper.TypeFilterPc, StringComparison.OrdinalIgnoreCase);
    public bool IsTypePs5 => string.Equals(SelectedTypeFilter, NetworkDevicesMapper.TypeFilterPs5, StringComparison.OrdinalIgnoreCase);

    public bool IsStatusAll => string.Equals(SelectedStatusFilter, NetworkDevicesMapper.StatusFilterAll, StringComparison.OrdinalIgnoreCase);
    public bool IsStatusAvailable => string.Equals(SelectedStatusFilter, "Available", StringComparison.OrdinalIgnoreCase);
    public bool IsStatusInUse => string.Equals(SelectedStatusFilter, "InUse", StringComparison.OrdinalIgnoreCase);
    public bool IsStatusOffline => string.Equals(SelectedStatusFilter, "Offline", StringComparison.OrdinalIgnoreCase);
    public bool IsStatusDisconnected => string.Equals(SelectedStatusFilter, "Disconnected", StringComparison.OrdinalIgnoreCase);
    public bool IsStatusMaintenance => string.Equals(SelectedStatusFilter, "Maintenance", StringComparison.OrdinalIgnoreCase);

    public NetworkDevicesSettingsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedTypeFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsTypeAll));
        OnPropertyChanged(nameof(IsTypePc));
        OnPropertyChanged(nameof(IsTypePs5));
        ApplyFilters();
    }

    partial void OnSelectedStatusFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsStatusAll));
        OnPropertyChanged(nameof(IsStatusAvailable));
        OnPropertyChanged(nameof(IsStatusInUse));
        OnPropertyChanged(nameof(IsStatusOffline));
        OnPropertyChanged(nameof(IsStatusDisconnected));
        OnPropertyChanged(nameof(IsStatusMaintenance));
        ApplyFilters();
    }

    [RelayCommand]
    public async Task EnsureLoadedAsync()
    {
        if (_loadedOnce && !HasLoadError)
        {
            return;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsDevicesLoading = true;
        HasLoadError = false;

        try
        {
            var devicesResult = await _apiClient.GetDevicesAsync();
            if (!devicesResult.IsSuccess || devicesResult.Data is null)
            {
                HasLoadError = true;
                _allDevices = new List<DeviceResponse>();
                Devices.Clear();
                UpdateCounters();
                UpdateStates();
                return;
            }

            _allDevices = devicesResult.Data.ToList();
            _loadedOnce = true;
            ApplyFilters();
            _ = LoadInfrastructureAsync();
        }
        catch
        {
            HasLoadError = true;
            _allDevices = new List<DeviceResponse>();
            Devices.Clear();
            UpdateCounters();
            UpdateStates();
        }
        finally
        {
            IsDevicesLoading = false;
            UpdateStates();
        }
    }

    [RelayCommand]
    private async Task LoadInfrastructureAsync()
    {
        IsInfrastructureLoading = true;
        try
        {
            var infoResult = await _apiClient.GetSystemInfoAsync();
            if (infoResult.IsSuccess && infoResult.Data is not null)
            {
                var info = infoResult.Data;
                MachineName = string.IsNullOrWhiteSpace(info.MachineName) ? "—" : info.MachineName;
                AppVersion = string.IsNullOrWhiteSpace(info.Version) ? "—" : info.Version;
                DatabaseProvider = string.IsNullOrWhiteSpace(info.DatabaseProvider) ? "—" : info.DatabaseProvider;
                ServerUrlsText = info.ServerUrls is { Count: > 0 }
                    ? string.Join("، ", info.ServerUrls)
                    : "—";
                CloudConfiguredText = info.CloudConfigured ? "پیکربندی شده" : "پیکربندی نشده";
            }
        }
        catch
        {
            // Infrastructure failure must not break the device table.
        }
        finally
        {
            IsInfrastructureLoading = false;
        }
    }

    [RelayCommand]
    private void RequestAddDevice()
    {
        AddDeviceNotice = "افزودن دستگاه در مرحله بعد پیاده‌سازی می‌شود.";
        IsAddDeviceNoticeVisible = true;
    }

    [RelayCommand]
    private void DismissAddDeviceNotice() => IsAddDeviceNoticeVisible = false;

    [RelayCommand]
    private void SetTypeFilter(string? filter) => SelectedTypeFilter = filter ?? string.Empty;

    [RelayCommand]
    private void SetStatusFilter(string? filter) => SelectedStatusFilter = filter ?? string.Empty;

    private void ApplyFilters()
    {
        var filtered = NetworkDevicesMapper.ApplyFilters(
            _allDevices,
            SearchText,
            SelectedTypeFilter,
            SelectedStatusFilter);

        Devices.Clear();
        foreach (var device in filtered)
        {
            Devices.Add(CreateRow(device));
        }

        UpdateCounters();
        UpdateStates();
    }

    private void UpdateStates()
    {
        IsEmpty = !HasLoadError && _allDevices.Count == 0;
        ShowTable = !HasLoadError && !IsDevicesLoading && Devices.Count > 0;
    }

    private void UpdateCounters()
    {
        var summary = NetworkDevicesMapper.BuildSummary(_allDevices);
        TotalDevicesText = ToPersian(summary.TotalDevices);
        PcCountText = ToPersian(summary.PcCount);
        Ps5CountText = ToPersian(summary.Ps5Count);
        ConnectedCountText = ToPersian(summary.ConnectedCount);
        DisconnectedCountText = ToPersian(summary.DisconnectedCount);
        MaintenanceCountText = ToPersian(summary.MaintenanceCount);
        VisibleCountText = ToPersian(Devices.Count);
    }

    private static NetworkDeviceRow CreateRow(DeviceResponse device)
        => new()
        {
            Id = device.Id,
            Name = string.IsNullOrWhiteSpace(device.Name) ? "—" : device.Name,
            TypeDisplay = NetworkDevicesMapper.MapTypeDisplay(device.Type),
            IpAddress = string.IsNullOrWhiteSpace(device.IpAddress) ? "—" : device.IpAddress,
            MacAddress = string.IsNullOrWhiteSpace(device.MacAddress) ? "—" : device.MacAddress,
            ClientIdentity = string.IsNullOrWhiteSpace(device.ClientIdentity) ? "—" : device.ClientIdentity,
            StatusWire = device.Status,
            StatusDisplay = NetworkDevicesMapper.MapStatusDisplay(device.Status),
            IsConnected = device.IsConnected,
            ConnectionDisplay = NetworkDevicesMapper.MapConnectionDisplay(device.IsConnected),
            LastHeartbeatDisplay = NetworkDevicesMapper.FormatHeartbeat(device.LastHeartbeat)
        };

    private static string ToPersian(int value)
    {
        var chars = value.ToString().ToCharArray();
        var digits = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = digits[chars[i] - '0'];
            }
        }

        return new string(chars);
    }
}

public sealed class NetworkDeviceRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TypeDisplay { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string ClientIdentity { get; init; } = string.Empty;
    public string StatusWire { get; init; } = string.Empty;
    public string StatusDisplay { get; init; } = string.Empty;
    public bool IsConnected { get; init; }
    public string ConnectionDisplay { get; init; } = string.Empty;
    public string LastHeartbeatDisplay { get; init; } = "—";

    public string StatusBrushHex => StatusWire switch
    {
        var s when s.Equals("Available", StringComparison.OrdinalIgnoreCase) => "#10B981",
        var s when s.Equals("InUse", StringComparison.OrdinalIgnoreCase) => "#4C8DFF",
        var s when s.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) => "#F59E0B",
        var s when s.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) => "#EF4444",
        var s when s.Equals("Offline", StringComparison.OrdinalIgnoreCase) => "#94A3B8",
        _ => "#94A3B8"
    };

    public string ConnectionBrushHex => IsConnected ? "#10B981" : "#EF4444";
}
