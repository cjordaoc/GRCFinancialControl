using System;
using Avalonia;
using Avalonia.Data.Converters;

namespace GRCFinancialControl.Avalonia.Converters;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolean)
        {
            return !boolean;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolean)
        {
            return !boolean;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
