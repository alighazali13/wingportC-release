using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GamePort.Cashier.ViewModels;

namespace GamePort.Cashier.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        SetError(null);

        _viewModel.Username = UsernameBox.Text;
        _viewModel.Password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(_viewModel.Username) || string.IsNullOrWhiteSpace(_viewModel.Password))
        {
            SetError("نام کاربری و رمز عبور را وارد کنید.");
            SetBusy(false);
            return;
        }

        try
        {
            _viewModel.ErrorMessage = null;

            await Task.Run(async () =>
            {
                if (_viewModel.LoginCommand.CanExecute(null))
                {
                    await _viewModel.LoginCommand.ExecuteAsync(null);
                }
            });

            Dispatcher.Invoke(() =>
            {
                if (_viewModel.ErrorMessage is not null)
                {
                    SetError(_viewModel.ErrorMessage);
                    SetBusy(false);
                    return;
                }

                DialogResult = true;
                Close();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                SetError($"خطا: {ex.Message}");
                SetBusy(false);
            });
        }
    }

    private void Field_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LoginButton.IsEnabled)
        {
            Login_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Password = PasswordBox.Password;
        }
    }

    private void SetError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            ErrorBorder.Visibility = Visibility.Visible;
            ErrorText.Text = message;
        }
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        LoadingText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
}
