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

    public ObservableCollection<GameCard> Games { get; } = new();

    [ObservableProperty]
    private string _activeTab = "Games";

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

    public SettingsViewModel(IApiClient apiClient)
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
            var gamesResult = await _apiClient.GetGamesAsync();
            if (!gamesResult.IsSuccess)
            {
                ErrorMessage = gamesResult.Error;
                return;
            }

            Games.Clear();
            var index = 0;

            foreach (var game in gamesResult.Data ?? new List<GameResponse>())
            {
                Games.Add(new GameCard
                {
                    Name = game.Name,
                    Badge = string.IsNullOrWhiteSpace(game.Version) ? game.SupportedDeviceType : game.Version!,
                    BadgeColor = game.IsActive ? "#0058BC" : "#717786",
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

            var infoResult = await _apiClient.GetSystemInfoAsync();
            if (infoResult.IsSuccess && infoResult.Data is not null)
            {
                Version = $"WingportS v{infoResult.Data.Version}";
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
    private void SetTab(string tab) => ActiveTab = tab;

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
