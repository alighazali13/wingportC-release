using System.Globalization;
using System.Windows.Data;

namespace GamePort.Cashier.Converters;

public class ProgressWidthConverter : IMultiValueConverter
{
    public static readonly ProgressWidthConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double parentWidth && values[1] is int progress)
        {
            return Math.Max(0, parentWidth * progress / 100.0);
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
