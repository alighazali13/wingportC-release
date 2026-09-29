using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GamePort.Cashier.Services;
using GamePort.Cashier.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GamePort.Cashier.Views;

public partial class LoginView : System.Windows.Controls.UserControl
{
    private readonly LoginViewModel _viewModel;
    private readonly ISessionContext _session;

    public Action? LoginSucceeded { get; set; }

    public LoginView()
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(
            App.ServiceProvider!.GetRequiredService<IApiClient>(),
            App.ServiceProvider.GetRequiredService<ISessionContext>());
        _session = App.ServiceProvider.GetRequiredService<ISessionContext>();
        DataContext = _viewModel;
        UsernameBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text?.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowStatus("نام کاربری و رمز عبور را وارد کنید.", false);
            return;
        }

        ShowStatus("در حال ورود...", true);

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
                ShowStatus(_viewModel.ErrorMessage, false);
                return;
            }

            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            ShowStatus("خطا: " + ex.Message, false);
        }
    }

    private void ShowStatus(string text, bool busy)
    {
        StatusText.Text = text;
        StatusBorder.Visibility = Visibility.Visible;
        LoginButton.IsEnabled = !busy;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoginButton_Click(sender, e);
    }
}
