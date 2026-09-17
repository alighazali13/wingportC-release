using CommunityToolkit.Mvvm.ComponentModel;

namespace GamePort.Cashier.Common;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    ObservableObject? CurrentViewModel { get; }
    event Action? CurrentViewModelChanged;
}

public class NavigationService : INavigationService
{
    private readonly Func<Type, ObservableObject> _viewModelFactory;
    private ObservableObject? _currentViewModel;

    public NavigationService(Func<Type, ObservableObject> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel != value)
            {
                _currentViewModel = value;
                CurrentViewModelChanged?.Invoke();
            }
        }
    }

    public event Action? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        CurrentViewModel = _viewModelFactory(typeof(TViewModel));
    }
}
