using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GRC.Shared.UI.Converters;

public sealed class BoolToDoubleConverter : IValueConverter
{
    public double TrueValue { get; set; }

    public double FalseValue { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            var result = flag ? TrueValue : FalseValue;

            if (targetType == typeof(object) || targetType == typeof(double) || targetType == typeof(double?))
            {
                return result;
            }

            if (targetType == typeof(string))
            {
                return result.ToString(culture);
            }
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
