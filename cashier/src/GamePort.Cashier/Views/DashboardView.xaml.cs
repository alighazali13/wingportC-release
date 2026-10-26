using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GamePort.Cashier.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewModels.DashboardViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(vm.FilteredDevices))
                {
                    EmptyState.Visibility = vm.FilteredDevices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                if (args.PropertyName == nameof(vm.RecentActivities))
                {
                    ActivityEmpty.Visibility = vm.RecentActivities.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            };
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                vm.SearchText = string.Empty;
            }
        }
    }
}
