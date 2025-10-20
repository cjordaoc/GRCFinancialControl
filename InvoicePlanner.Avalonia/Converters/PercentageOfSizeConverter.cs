using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace InvoicePlanner.Avalonia.Converters;

public class PercentageOfSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double size && TryParsePercentage(parameter, out var percentage))
        {
            return size * percentage;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static bool TryParsePercentage(object? parameter, out double percentage)
    {
        switch (parameter)
        {
            case null:
                percentage = 1d;
                return true;
            case double d:
                percentage = d;
                return true;
            case float f:
                percentage = f;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                percentage = parsed;
                return true;
            default:
                percentage = 1d;
                return false;
        }
    }
}
