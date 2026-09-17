using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GamePort.Cashier.Common;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISessionContext _session;

    [ObservableProperty]
    private string _pageTitle = "داشبورد مدیریت";

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private bool _isPinModalVisible;

    public MainWindowViewModel(INavigationService navigationService, ISessionContext session)
    {
        _navigationService = navigationService;
        _session = session;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;

        WeakReferenceMessenger.Default.Register<OpenPinModalMessage>(this, (recipient, message) =>
        {
            IsPinModalVisible = true;
        });

        NavigateToDashboard();
    }

    public event Action? LogoutRequested;

    public string EmployeeName => string.IsNullOrWhiteSpace(_session.Name) ? "کارمند" : _session.Name;

    public string EmployeeRole => _session.Role ?? string.Empty;

    private void OnCurrentViewModelChanged()
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        PageTitle = "داشبورد مدیریت";
        CurrentPage = "Dashboard";
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    [RelayCommand]
    private void NavigateToDevices()
    {
        PageTitle = "مدیریت سیستم‌ها";
        CurrentPage = "Devices";
        _navigationService.NavigateTo<DevicesViewModel>();
    }

    [RelayCommand]
    private void NavigateToFinancialReports()
    {
        PageTitle = "گزارشات مالی";
        CurrentPage = "Financial";
        _navigationService.NavigateTo<FinancialReportsViewModel>();
    }

    [RelayCommand]
    private void NavigateToUsers()
    {
        PageTitle = "مدیریت کاربران";
        CurrentPage = "Users";
        _navigationService.NavigateTo<UsersViewModel>();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        PageTitle = "تنظیمات سیستم";
        CurrentPage = "Settings";
        _navigationService.NavigateTo<SettingsViewModel>();
    }

    [RelayCommand]
    private void ClosePinModal()
    {
        IsPinModalVisible = false;
    }

    [RelayCommand]
    private void ConfirmPin()
    {
        IsPinModalVisible = false;
        WeakReferenceMessenger.Default.Send(new PinConfirmedMessage());
    }

    [RelayCommand]
    private void Logout()
    {
        _session.SignOut();
        LogoutRequested?.Invoke();
    }
}
