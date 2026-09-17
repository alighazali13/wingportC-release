using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GamePort.Cashier.Converters;

public class ActiveNavBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value as string;
        var target = parameter as string;
        return current == target
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0x4A, 0xCA))
            : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ActiveNavForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value as string;
        var target = parameter as string;
        return current == target
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(0x41, 0x47, 0x55));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
