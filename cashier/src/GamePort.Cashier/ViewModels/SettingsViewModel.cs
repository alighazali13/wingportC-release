using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamePort.Cashier.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] CoverPalette =
    {
        "#1E293B", "#065F46", "#7F1D1D", "#78350F", "#1E40AF", "#4C1D95"
    };

    private readonly IApiClient _apiClient;
    private readonly ISessionContext _sessionContext;

    public ObservableCollection<GameCard> Games { get; } = new();
    public ObservableCollection<SettingsCategory> Categories { get; } = new();

    public NetworkDevicesSettingsViewModel NetworkDevices { get; }

    [ObservableProperty]
    private string _activeSection = "Organization";

    [ObservableProperty]
    private string _selectedTheme = "Light";

    [ObservableProperty]
    private string _selectedAccent = "#0058BC";

    [ObservableProperty]
    private bool _isAddGameFormVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _version = "WingportS";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    // Real operational data
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pcCount;

    [ObservableProperty]
    private int _psCount;

    [ObservableProperty]
    private string _cloudStatus = "نامشخص";

    [ObservableProperty]
    private string _databaseProvider = "نامشخص";

    [ObservableProperty]
    private string _machineName = "نامشخص";

    [ObservableProperty]
    private string _userRole = "کاربر";

    [ObservableProperty]
    private string _userName = "";

    public SettingsViewModel(IApiClient apiClient, ISessionContext sessionContext)
    {
        _apiClient = apiClient;
        _sessionContext = sessionContext;
        NetworkDevices = new NetworkDevicesSettingsViewModel(apiClient);
        BuildCategories();
        _ = LoadAsync();
    }

    private void BuildCategories()
    {
        Categories.Clear();

        // Exact Stitch order and properties
        Categories.Add(new SettingsCategory
        {
            Key = "Organization",
            Label = "مجموعه",
            Icon = "\uEAF0", // apartment
            IsLocked = false,
            Badge = "",
            IsSelected = true
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Halls",
            Label = "سالن‌ها و بخش‌ها",
            Icon = "\uE0A9", // meeting_room
            IsLocked = false,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Pricing",
            Label = "تعرفه‌ها و قیمت‌گذاری",
            Icon = "\uE227", // price_change
            IsLocked = true,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Reservations",
            Label = "رزروها و نشست‌ها",
            Icon = "\uE329", // event_seat
            IsLocked = false,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Customers",
            Label = "مشتریان، کیف پول و امتیاز",
            Icon = "\uE0A2", // loyalty
            IsLocked = false,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Payments",
            Label = "صندوق و پرداخت",
            Icon = "\uE8A1", // point_of_sale
            IsLocked = true,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "StaffGuest",
            Label = "مهمان پرسنل",
            Icon = "\uE7FB", // badge
            IsLocked = true,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Network",
            Label = "شبکه و دستگاه‌ها",
            Icon = "\uE30A", // lan / computer
            IsLocked = false,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "CloudSync",
            Label = "Cloud Sync و پشتیبان‌گیری",
            Icon = "\uE2C3", // cloud_sync
            IsLocked = false,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Security",
            Label = "امنیت و دسترسی‌ها",
            Icon = "\uE32A", // shield
            IsLocked = true,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "Printing",
            Label = "چاپ و رسید",
            Icon = "\uE8AD", // print
            IsLocked = false,
            Badge = ""
        });
        Categories.Add(new SettingsCategory
        {
            Key = "General",
            Label = "تنظیمات عمومی",
            Icon = "\uE8B8", // tune / settings
            IsLocked = false,
            Badge = ""
        });
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Session info (real)
            if (_sessionContext.IsAuthenticated)
            {
                UserName = _sessionContext.Name ?? "";
                UserRole = _sessionContext.Role ?? "کاربر";
            }

            // System info (real)
            var infoResult = await _apiClient.GetSystemInfoAsync();
            if (infoResult.IsSuccess && infoResult.Data is not null)
            {
                Version = string.IsNullOrWhiteSpace(infoResult.Data.Version)
                    ? "WingportS"
                    : $"v{infoResult.Data.Version}";
                DatabaseProvider = string.IsNullOrWhiteSpace(infoResult.Data.DatabaseProvider)
                    ? "نامشخص"
                    : infoResult.Data.DatabaseProvider;
                MachineName = string.IsNullOrWhiteSpace(infoResult.Data.MachineName)
                    ? "نامشخص"
                    : infoResult.Data.MachineName;
            }

            // Device counts (real)
            var devicesResult = await _apiClient.GetDevicesAsync();
            if (devicesResult.IsSuccess && devicesResult.Data is not null)
            {
                var devices = devicesResult.Data;
                TotalCount = devices.Count;
                PcCount = devices.Count(d => d.Type.Equals("GamingPC", StringComparison.OrdinalIgnoreCase));
                PsCount = devices.Count(d => d.Type.Equals("PlayStation", StringComparison.OrdinalIgnoreCase));
            }

            // Cloud sync status (real)
            var syncResult = await _apiClient.GetSyncStatusAsync();
            if (syncResult.IsSuccess && syncResult.Data is not null)
            {
                CloudStatus = syncResult.Data.CloudConfigured ? "متصل" : "پیکربندی نشده";
            }

            // Games (preserved from existing)
            var gamesResult = await _apiClient.GetGamesAsync();
            if (gamesResult.IsSuccess)
            {
                Games.Clear();
                var index = 0;

                foreach (var game in gamesResult.Data ?? new List<GameResponse>())
                {
                    Games.Add(new GameCard
                    {
                        Name = game.Name,
                        Badge = string.IsNullOrWhiteSpace(game.Version) ? game.SupportedDeviceType : game.Version!,
                        BadgeColor = game.IsActive ? "#4C8DFF" : "#64748B",
                        Platform = $"پلتفرم: {game.SupportedDeviceType}",
                        PlatformIcon = game.SupportedDeviceType.Equals("PlayStation", StringComparison.OrdinalIgnoreCase) ? "\uEA28" : "\uE30A",
                        SystemsCount = game.IsActive ? "فعال" : "غیرفعال",
                        ExecPath = game.ExecutablePath ?? "-",
                        Status = game.IsActive ? "آماده اجرا" : "غیرفعال",
                        NeedsUpdate = !game.IsActive,
                        CoverFrom = ToColor(CoverPalette[index % CoverPalette.Length]),
                        CoverTo = ToColor(CoverPalette[(index + 2) % CoverPalette.Length])
                    });

                    index++;
                }
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
    private void SetSection(string section)
    {
        ActiveSection = section;
        foreach (var cat in Categories)
            cat.IsSelected = cat.Key == section;

        if (section == "Network")
        {
            _ = NetworkDevices.EnsureLoadedAsync();
        }
    }

    [RelayCommand]
    private void SetTheme(string theme) => SelectedTheme = theme;

    [RelayCommand]
    private void SetAccent(string color) => SelectedAccent = color;

    [RelayCommand]
    private void ToggleAddGame() => IsAddGameFormVisible = !IsAddGameFormVisible;

    [RelayCommand]
    private void Save()
    {
        StatusMessage = "تنظیمات محلی ذخیره شد.";
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedTheme = "Light";
        SelectedAccent = "#0058BC";
        StatusMessage = "تنظیمات به حالت پیش‌فرض بازگشت.";
    }

    [RelayCommand]
    private async Task SyncClientsAsync()
    {
        StatusMessage = "در حال همگام‌سازی...";

        var result = await _apiClient.RunSyncAsync();
        StatusMessage = result.IsSuccess
            ? "همگام‌سازی با Cloud انجام شد."
            : $"همگام‌سازی انجام نشد: {result.Error}";
    }

    private static Color ToColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}

public partial class SettingsCategory : ObservableObject
{
    private string _key = string.Empty;
    private string _label = string.Empty;
    private string _icon = string.Empty;
    private bool _isLocked;
    private string _badge = string.Empty;
    private bool _isSelected;

    public string Key { get => _key; set => SetProperty(ref _key, value); }
    public string Label { get => _label; set => SetProperty(ref _label, value); }
    public string Icon { get => _icon; set => SetProperty(ref _icon, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
    public string Badge { get => _badge; set => SetProperty(ref _badge, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

public class GameCard
{
    public string Name { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PlatformIcon { get; set; } = string.Empty;
    public string SystemsCount { get; set; } = string.Empty;
    public string ExecPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool NeedsUpdate { get; set; }
    public Color CoverFrom { get; set; }
    public Color CoverTo { get; set; }
}
