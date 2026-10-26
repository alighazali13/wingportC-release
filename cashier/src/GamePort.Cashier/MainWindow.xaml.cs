using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using GamePort.Cashier.Common;
using GamePort.Cashier.Services;
using GamePort.Cashier.Services.Updates;
using GamePort.Cashier.ViewModels;
using GamePort.Cashier.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier;

public partial class MainWindow : Window
{
    private readonly ISessionContext _session;
    private readonly INavigationService _navigation;
    private MainWindowViewModel? _viewModel;
    private IAppUpdateService? _updateService;
    private DispatcherTimer? _clockTimer;

    public MainWindow(ISessionContext session, INavigationService navigation)
    {
        _session = session;
        _navigation = navigation;
        InitializeComponent();
        _navigation.CurrentViewModelChanged += OnCurrentViewModelChanged;

        var loginView = App.ServiceProvider.GetRequiredService<LoginView>();
        loginView.LoginSucceeded = LoginViewControl_LoginSucceeded;
        LoginContent.Content = loginView;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TopBarClock.Text = DateTime.Now.ToString("HH:mm:ss");

        try
        {
            _updateService = App.ServiceProvider.GetService<IAppUpdateService>();
            if (_updateService is not null)
            {
                _updateService.StateChanged += OnUpdateStateChanged;
            }
        }
        catch { /* update service unavailable — non-fatal */ }
    }

    #region Custom title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch { /* drag interrupted — ignore */ }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => SystemCommands.MinimizeWindow(this);

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximizeRestore();

    private void Close_Click(object sender, RoutedEventArgs e)
        => SystemCommands.CloseWindow(this);

    private void ToggleMaximizeRestore()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Swap maximize / restore icon path
        if (BtnMaximize?.Content is System.Windows.Shapes.Path path)
        {
            if (WindowState == WindowState.Maximized)
            {
                // Restore: overlapping squares
                path.Data = Geometry.Parse("M 0,3 H 7 V 10 H 0 Z M 3,0 H 10 V 7 H 3 Z");
                BtnMaximize.ToolTip = "Restore";
                AutomationProperties.SetName(BtnMaximize, "Restore");
            }
            else
            {
                // Maximize: single square
                path.Data = Geometry.Parse("M 0,0 H 10 V 10 H 0 Z");
                BtnMaximize.ToolTip = "Maximize";
                AutomationProperties.SetName(BtnMaximize, "Maximize");
            }
        }
    }

    #endregion

    private void LoginViewControl_LoginSucceeded()
    {
        LoginContent.Visibility = Visibility.Collapsed;
        ContentArea.Visibility = Visibility.Visible;

        _viewModel = App.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        _viewModel.LogoutRequested += OnLogoutRequested;
        DataContext = _viewModel;

        TopBarEmployeeName.Text = _viewModel.EmployeeName;

        _clockTimer?.Start();
        TopBarClock.Text = DateTime.Now.ToString("HH:mm:ss");

        _viewModel.NavigateToDashboard();
        UpdateNavHighlight("Dashboard");
    }

    private void OnCurrentViewModelChanged()
    {
        MainContent.Content = _navigation.CurrentViewModel;
    }

    private void OnLogoutRequested()
    {
        _clockTimer?.Stop();
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

    #region Update Notification

    private void OnUpdateStateChanged()
    {
        Dispatcher.Invoke(() =>
        {
            if (_updateService?.State == UpdateState.Ready
                && _updateService.AvailableVersion is not null
                && ContentArea.Visibility == Visibility.Visible)
            {
                UpdateCurrentVer.Text = _updateService.CurrentVersion;
                UpdateNewVer.Text = _updateService.AvailableVersion;
                UpdateBanner.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateBanner.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void UpdateApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _updateService?.ApplyUpdateAndRestart();
        }
        catch { /* apply failure is non-fatal */ }
    }

    private void UpdateDismiss_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
    }

    #endregion
}
