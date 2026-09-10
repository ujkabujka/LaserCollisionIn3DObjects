using System.Globalization;
using System.Windows.Data;

namespace LaserCollisionIn3DObjects.Wpf.Converters;

public sealed class PanelResultLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var ordinal = values.ElementAtOrDefault(0) is int value ? value : 1;
        var panelName = values.ElementAtOrDefault(1)?.ToString() ?? string.Empty;
        var hitCount = values.ElementAtOrDefault(2) is int count ? count : 0;
        return $"S{ordinal} {panelName} - {hitCount} hits";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
