using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GamePort.Cashier.Views;

/// <summary>
/// Returns Visible when the bound ActiveSection matches the target section key.
/// </summary>
public class SectionVisibilityConverter : IValueConverter
{
    private readonly string _targetKey;

    public SectionVisibilityConverter(string targetKey)
    {
        _targetKey = targetKey;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string current && current == _targetKey
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns Visible only for sections that do NOT have a dedicated converter.
/// Covers: Halls, Pricing, Reservations, Customers, Payments, StaffGuest, Security, Printing.
/// </summary>
public class OtherSectionVisibilityConverter : IValueConverter
{
    private static readonly HashSet<string> DedicatedSections = new()
    {
        "Organization", "Network", "CloudSync", "General"
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string current && !DedicatedSections.Contains(current)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Centralized converter instances for Settings section switching.
/// </summary>
public static class SettingsConverters
{
    public static readonly SectionVisibilityConverter Organization = new("Organization");
    public static readonly SectionVisibilityConverter Network = new("Network");
    public static readonly SectionVisibilityConverter CloudSync = new("CloudSync");
    public static readonly SectionVisibilityConverter General = new("General");
    public static readonly OtherSectionVisibilityConverter Other = new();
}
