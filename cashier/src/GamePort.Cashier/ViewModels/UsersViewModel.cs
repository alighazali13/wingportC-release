using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamePort.Cashier.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private readonly List<UserListItem> _allUsers = new();

    public ObservableCollection<UserListItem> FilteredUsers { get; } = new();

    [ObservableProperty]
    private UserListItem? _selectedUser;

    [ObservableProperty]
    private string _totalUsers = "۰";

    [ObservableProperty]
    private string _onlineUsers = "۰";

    [ObservableProperty]
    private string _walletTotal = "۰";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _activeTier = "All";

    [ObservableProperty]
    private string _resultCount = string.Empty;

    [ObservableProperty]
    private bool _hasResults = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public UsersViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnActiveTierChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var customersResult = await _apiClient.SearchCustomersAsync(null);
            if (!customersResult.IsSuccess)
            {
                ErrorMessage = customersResult.Error;
                return;
            }

            var sessionsResult = await _apiClient.GetActiveSessionsAsync();
            var reservationsResult = await _apiClient.GetUpcomingReservationsAsync();

            var customers = customersResult.Data ?? new List<CustomerResponse>();
            var sessions = sessionsResult.Data ?? new List<SessionResponse>();
            var reservations = reservationsResult.Data ?? new List<ReservationResponse>();

            _allUsers.Clear();

            var walletTotal = 0m;
            var onlineCount = 0;

            foreach (var customer in customers.OrderBy(c => c.Name))
            {
                var session = sessions.FirstOrDefault(s => s.CustomerId == customer.Id);
                var reservation = reservations.FirstOrDefault(r => r.CustomerId == customer.Id);
                var balance = customer.WalletBalance ?? 0m;
                walletTotal += balance;

                if (session is not null)
                {
                    onlineCount++;
                }

                var item = new UserListItem
                {
                    Name = customer.Name,
                    GamerTag = string.IsNullOrWhiteSpace(customer.PhoneNumber) ? "-" : customer.PhoneNumber,
                    UserId = $"#WP-{customer.Id.ToString("N")[..6].ToUpperInvariant()}",
                    Tier = "عادی",
                    Initials = BuildInitials(customer.Name),
                    Phone = PersianFormat.Digits(customer.PhoneNumber ?? "-"),
                    Balance = PersianFormat.Number(balance),
                    Hours = "-",
                    TotalTime = "-",
                    JoinDate = PersianFormat.Digits(customer.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd")),
                    Discount = "-",
                    LoyaltyPoints = "۰",
                    StatusText = session is not null
                        ? "در حال بازی"
                        : reservation is not null
                            ? "رزرو فعال"
                            : "آفلاین",
                    StatusType = session is not null
                        ? UserStatus.Playing
                        : reservation is not null
                            ? UserStatus.Reserved
                            : UserStatus.Offline,
                    IsOnline = session is not null,
                    IsLowBalance = balance <= 0,
                    DeviceInfo = session is not null ? "در حال بازی" : "-",
                    FavoriteSystem = "-",
                    Rank = "-",
                    MonthlyHours = "-"
                };

                _allUsers.Add(item);
            }

            TotalUsers = PersianFormat.Number(customers.Count);
            OnlineUsers = PersianFormat.Number(onlineCount);
            WalletTotal = PersianFormat.Number(walletTotal);

            ApplyFilter();
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
    private void SetTierFilter(string tier) => ActiveTier = tier;

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void AddUser()
    {
    }

    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        return parts.Length == 1
            ? parts[0][..1]
            : $"{parts[0][..1]}{parts[^1][..1]}";
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        FilteredUsers.Clear();
        foreach (var u in _allUsers)
        {
            if (!string.IsNullOrEmpty(query) &&
                !(u.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                  u.GamerTag.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                  u.Phone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                  u.UserId.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (ActiveTier != "All" && u.Tier != ActiveTier)
            {
                continue;
            }

            FilteredUsers.Add(u);
        }

        HasResults = FilteredUsers.Count > 0;
        ResultCount = PersianFormat.Digits($"{FilteredUsers.Count} نفر نمایش داده شده");

        if (SelectedUser is null || !FilteredUsers.Contains(SelectedUser))
        {
            SelectedUser = FilteredUsers.FirstOrDefault();
        }
    }
}

public enum UserStatus { Playing, Reserved, Offline }

public enum SessionType { Active, Settled, Recharge }

public partial class UserListItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string GamerTag { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Balance { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;
    public string TotalTime { get; set; } = string.Empty;
    public string JoinDate { get; set; } = string.Empty;
    public string Discount { get; set; } = string.Empty;
    public string LoyaltyPoints { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public UserStatus StatusType { get; set; }
    public bool IsOnline { get; set; }
    public string DeviceInfo { get; set; } = string.Empty;
    public string FavoriteSystem { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string MonthlyHours { get; set; } = string.Empty;
    public bool IsTrusted { get; set; }
    public bool IsLowBalance { get; set; }

    public ObservableCollection<FavoriteGame> FavoriteGames { get; } = new();
    public ObservableCollection<UserSession> Sessions { get; } = new();
}

public class FavoriteGame
{
    public string Name { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;
    public int Percent { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class UserSession
{
    public string Device { get; set; } = string.Empty;
    public string DateTime { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public SessionType SessionType { get; set; }
}
