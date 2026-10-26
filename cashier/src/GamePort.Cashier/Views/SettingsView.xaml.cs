using System.Windows;
using System.Windows.Controls;
using GamePort.Cashier.ViewModels;

namespace GamePort.Cashier.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void Category_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string key &&
            DataContext is SettingsViewModel vm)
        {
            vm.SetSectionCommand.Execute(key);
        }
    }
}
