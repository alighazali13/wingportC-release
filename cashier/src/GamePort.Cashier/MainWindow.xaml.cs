using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GamePort.Cashier.Common;
using GamePort.Cashier.Services;
using GamePort.Cashier.ViewModels;
using GamePort.Cashier.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier;

public partial class MainWindow : Window
{
    private readonly ISessionContext _session;
    private readonly INavigationService _navigation;
    private MainWindowViewModel? _viewModel;

    public MainWindow(ISessionContext session, INavigationService navigation)
    {
        _session = session;
        _navigation = navigation;
        InitializeComponent();
        _navigation.CurrentViewModelChanged += OnCurrentViewModelChanged;

        var loginView = App.ServiceProvider.GetRequiredService<LoginView>();
        loginView.LoginSucceeded = LoginViewControl_LoginSucceeded;
        LoginContent.Content = loginView;
    }

    private void LoginViewControl_LoginSucceeded()
    {
        LoginContent.Visibility = Visibility.Collapsed;
        ContentArea.Visibility = Visibility.Visible;

        _viewModel = App.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        _viewModel.LogoutRequested += OnLogoutRequested;
        DataContext = _viewModel;

        EmployeeNameText.Text = _viewModel.EmployeeName;

        _viewModel.NavigateToDashboard();
        UpdateNavHighlight("Dashboard");
    }

    private void OnCurrentViewModelChanged()
    {
        MainContent.Content = _navigation.CurrentViewModel;
    }

    private void OnLogoutRequested()
    {
        ContentArea.Visibility = Visibility.Collapsed;
        LoginContent.Visibility = Visibility.Visible;

        _viewModel = null;
        DataContext = null;
        MainContent.Content = null;

        var loginView = App.ServiceProvider.GetRequiredService<LoginView>();
        loginView.LoginSucceeded = LoginViewControl_LoginSucceeded;
        LoginContent.Content = loginView;
        Title = "وینگ‌پورت";
    }

    private void NavDashboard_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.NavigateToDashboard();
        UpdateNavHighlight("Dashboard");
    }

    private void NavDevices_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.NavigateToDevices();
        UpdateNavHighlight("Devices");
    }

    private void NavUsers_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.NavigateToUsers();
        UpdateNavHighlight("Users");
    }

    private void NavFinancial_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.NavigateToFinancialReports();
        UpdateNavHighlight("Financial");
    }

    private void NavSettings_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.NavigateToSettings();
        UpdateNavHighlight("Settings");
    }

    private void NavLogout_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.Logout();
    }

    private void UpdateNavHighlight(string page)
    {
        var activeBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        NavDashboard.Background = page == "Dashboard" ? activeBrush : Brushes.Transparent;
        NavDevices.Background = page == "Devices" ? activeBrush : Brushes.Transparent;
        NavUsers.Background = page == "Users" ? activeBrush : Brushes.Transparent;
        NavFinancial.Background = page == "Financial" ? activeBrush : Brushes.Transparent;
        NavSettings.Background = page == "Settings" ? activeBrush : Brushes.Transparent;
    }
}
