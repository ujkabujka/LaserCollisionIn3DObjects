using System.Globalization;
using System.Windows.Data;

namespace LaserCollisionIn3DObjects.Wpf.Converters;

public sealed class FlexibleNumericConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            double number => number.ToString("G", CultureInfo.InvariantCulture),
            float number => number.ToString("G", CultureInfo.InvariantCulture),
            double? nullableNumber when nullableNumber.HasValue => nullableNumber.Value.ToString("G", CultureInfo.InvariantCulture),
            float? nullableNumber when nullableNumber.HasValue => nullableNumber.Value.ToString("G", CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim() ?? string.Empty;
        var isNullable = Nullable.GetUnderlyingType(targetType) is not null;
        var numericType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (string.IsNullOrEmpty(text))
        {
            return isNullable ? null! : Binding.DoNothing;
        }

        if (IsIntermediateValue(text))
        {
            return Binding.DoNothing;
        }

        if (!TryParseFlexible(text, out var parsed))
        {
            return Binding.DoNothing;
        }

        if (numericType == typeof(double))
        {
            return parsed;
        }

        if (numericType == typeof(float))
        {
            return (float)parsed;
        }

        return Binding.DoNothing;
    }

    private static bool IsIntermediateValue(string text)
        => text is "-" or "+" or "." or "," or "-." or "-," or "+." or "+,";

    private static bool TryParseFlexible(string text, out double parsed)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            return true;
        }

        var normalized = text.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }
}
