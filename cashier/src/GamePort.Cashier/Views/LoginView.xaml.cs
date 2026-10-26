using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GamePort.Cashier.Services;
using GamePort.Cashier.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier.Views;

public partial class LoginView : System.Windows.Controls.UserControl
{
    private readonly LoginViewModel _viewModel;
    private readonly ISessionContext _session;
    private readonly DispatcherTimer _clockTimer;
    private bool _isPasswordVisible;
    private bool _suppressPasswordSync;

    public Action? LoginSucceeded { get; set; }

    public LoginView()
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(
            App.ServiceProvider!.GetRequiredService<IApiClient>(),
            App.ServiceProvider.GetRequiredService<ISessionContext>());
        _session = App.ServiceProvider.GetRequiredService<ISessionContext>();
        DataContext = _viewModel;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        // Set dynamic version indicator
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
        var ver = info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "unknown";
        VersionText.Text = $"GamePort Cashier • v{ver}";

        UsernameBox.Focus();
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    // ========== ADAPTIVE LAYOUT (Width + Height) ==========

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var w = e.NewSize.Width;
        var h = e.NewSize.Height;

        // --- WIDTH adaptive ---
        if (w >= 1100)
        {
            AuthCol.MinWidth = 430;
            AuthCol.MaxWidth = 540;
            AuthPanel.Padding = new Thickness(28, 20, 28, 20);
            VisualPanel.Padding = new Thickness(28);
        }
        else if (w >= 900)
        {
            AuthCol.MinWidth = 400;
            AuthCol.MaxWidth = 480;
            AuthPanel.Padding = new Thickness(24, 18, 24, 18);
            VisualPanel.Padding = new Thickness(20);
        }
        else
        {
            AuthCol.MinWidth = 370;
            AuthCol.MaxWidth = 440;
            AuthPanel.Padding = new Thickness(20, 16, 20, 16);
            VisualPanel.Padding = new Thickness(16);
        }

        // --- HEIGHT adaptive ---
        // At reduced height: compact the infrastructure card, hide low-priority details
        if (h >= 750)
        {
            // Large height: full infrastructure
            OfflineBadge.Visibility = Visibility.Visible;
            SyncRow.Visibility = Visibility.Visible;
            OfflineNote.Visibility = Visibility.Visible;
            FooterSection.Visibility = Visibility.Visible;
            InfraSection.Visibility = Visibility.Visible;
        }
        else if (h >= 650)
        {
            // Medium height: keep infrastructure, hide footer
            OfflineBadge.Visibility = Visibility.Visible;
            SyncRow.Visibility = Visibility.Visible;
            OfflineNote.Visibility = Visibility.Visible;
            FooterSection.Visibility = Visibility.Collapsed;
            InfraSection.Visibility = Visibility.Visible;
        }
        else
        {
            // Compact height: minimal infrastructure
            OfflineBadge.Visibility = Visibility.Collapsed;
            SyncRow.Visibility = Visibility.Collapsed;
            OfflineNote.Visibility = Visibility.Collapsed;
            FooterSection.Visibility = Visibility.Collapsed;
            InfraSection.Visibility = Visibility.Visible;
        }

        // --- WIDTH + HEIGHT combined: hide visual decoration at small sizes ---
        if (w < 900 || h < 650)
        {
            HeroVisual.Visibility = Visibility.Collapsed;
            MicroBranding.Visibility = Visibility.Collapsed;
            TerminalIdText.Visibility = Visibility.Collapsed;
            LiveStatusBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            HeroVisual.Visibility = Visibility.Visible;
            MicroBranding.Visibility = Visibility.Visible;
            LiveStatusBar.Visibility = Visibility.Visible;

            if (w >= 1100)
                TerminalIdText.Visibility = Visibility.Visible;
            else
                TerminalIdText.Visibility = Visibility.Collapsed;
        }
    }

    // ========== AUTHENTICATION ==========

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text?.Trim();
        var password = GetCurrentPassword();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("نام کاربری و رمز عبور را وارد کنید.");
            return;
        }

        SetLoadingState(true);

        try
        {
            _viewModel.Username = username;
            _viewModel.Password = password;

            await Task.Run(async () =>
            {
                if (_viewModel.LoginCommand.CanExecute(null))
                    await _viewModel.LoginCommand.ExecuteAsync(null);
            });

            if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                ShowError(_viewModel.ErrorMessage);
                return;
            }

            LoginIcon.Text = "\xE86C";
            LoginButtonText.Text = "احراز هویت موفق";
            LoginEnterHint.Visibility = Visibility.Collapsed;
            LoginButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0x99));

            _clockTimer.Stop();
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            ShowError("خطا: " + ex.Message);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void SetLoadingState(bool loading)
    {
        LoginButton.IsEnabled = !loading;
        LoadingIndicator.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        LoginButtonText.Text = loading ? "در حال اعتبارسنجی..." : "ورود به سیستم";
        LoginEnterHint.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorAlert.Visibility = Visibility.Visible;
    }

    // ========== PASSWORD VISIBILITY ==========

    private string GetCurrentPassword()
    {
        return _isPasswordVisible ? PasswordRevealBox.Text : PasswordBox.Password;
    }

    private void PasswordToggle_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        _suppressPasswordSync = true;
        try
        {
            if (_isPasswordVisible)
            {
                PasswordRevealBox.Text = PasswordBox.Password;
                PasswordRevealBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                EyeIcon.Text = "\xE8F5";
            }
            else
            {
                PasswordBox.Password = PasswordRevealBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordRevealBox.Visibility = Visibility.Collapsed;
                EyeIcon.Text = "\xE8F4";
            }
        }
        finally
        {
            _suppressPasswordSync = false;
        }

        if (_isPasswordVisible)
            PasswordRevealBox.Focus();
        else
            PasswordBox.Focus();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressPasswordSync || !_isPasswordVisible) return;
        PasswordRevealBox.Text = PasswordBox.Password;
    }

    private void PasswordRevealBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressPasswordSync || _isPasswordVisible) return;
        PasswordBox.Password = PasswordRevealBox.Text;
    }

    // ========== KEYBOARD ==========

    private void UsernameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_isPasswordVisible)
                PasswordRevealBox.Focus();
            else
                PasswordBox.Focus();
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoginButton_Click(sender, e);
    }
}
