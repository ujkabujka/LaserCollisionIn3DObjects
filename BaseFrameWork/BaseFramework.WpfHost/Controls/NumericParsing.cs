using System.Globalization;

namespace BaseFramework.WpfHost.Controls;

internal static class NumericParsing
{
    internal static bool TryParseFlexibleDouble(string? text, out double value)
    {
        value = 0d;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
    }
}
