using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private readonly ISessionContext _session;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(IApiClient apiClient, ISessionContext session)
    {
        _apiClient = apiClient;
        _session = session;
    }

    public event Action? LoginSucceeded;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "نام کاربری و رمز عبور را وارد کنید.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiClient.LoginAsync(Username.Trim(), Password);

            if (!result.IsSuccess || result.Data is null)
            {
                ErrorMessage = result.Error ?? "ورود ناموفق بود.";
                return;
            }

            var data = result.Data;
            _session.SignIn(data.Token, data.EmployeeId, data.Name, data.Username, data.Role);
            LoginSucceeded?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
