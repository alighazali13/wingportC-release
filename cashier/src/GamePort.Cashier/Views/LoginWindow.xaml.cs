using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GamePort.Cashier.Services;
using GamePort.Cashier.ViewModels;

namespace GamePort.Cashier.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Title = "ورود به وینگ‌پورت";
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ShowStatus("نام کاربری و رمز عبور را وارد کنید.", false);
            return;
        }

        ShowStatus("در حال ورود...", true);

        try
        {
            _viewModel.Username = UsernameBox.Text.Trim();
            _viewModel.Password = PasswordBox.Password;
            _viewModel.ErrorMessage = null;

            await Task.Run(async () =>
            {
                if (_viewModel.LoginCommand.CanExecute(null))
                    await _viewModel.LoginCommand.ExecuteAsync(null);
            });

            if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                ShowStatus(_viewModel.ErrorMessage, false);
                return;
            }

            Dispatcher.Invoke(() =>
            {
                DialogResult = true;
                Close();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ShowStatus("خطا: " + ex.Message, false));
        }
    }

    private void ShowStatus(string text, bool isBusy)
    {
        StatusText.Text = text;
        StatusBorder.Visibility = Visibility.Visible;
        LoginButton.IsEnabled = !isBusy;
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoginButton_Click(sender, e);
    }
}
