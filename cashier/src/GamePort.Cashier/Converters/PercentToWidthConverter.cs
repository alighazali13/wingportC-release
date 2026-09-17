using System.Globalization;
using System.Windows.Data;

namespace GamePort.Cashier.Converters;

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double maxWidth = 300;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            maxWidth = m;

        if (value is int percent)
            return Math.Max(0, maxWidth * percent / 100.0);

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
