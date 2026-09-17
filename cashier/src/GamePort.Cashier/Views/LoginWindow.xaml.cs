using System.Windows;
using System.Windows.Input;
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
        _viewModel.LoginSucceeded += OnLoginSucceeded;

        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void OnLoginSucceeded()
    {
        if (Dispatcher.CheckAccess())
        {
            DialogResult = true;
            Close();
        }
        else
        {
            Dispatcher.Invoke(() =>
            {
                DialogResult = true;
                Close();
            });
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private void Field_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.LoginCommand.CanExecute(null))
        {
            _viewModel.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
