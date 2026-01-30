using System;
using Avalonia;
using Avalonia.Data.Converters;

namespace GRC.Shared.UI.Converters;

/// <summary>
/// Inverts boolean values for binding scenarios where negation is needed.
/// Commonly used for IsEnabled inversions (e.g., disable button while operation is running).
/// </summary>
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
