using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace GRC.Shared.UI.Converters;

public sealed class BoolToHorizontalAlignmentConverter : IValueConverter
{
    public HorizontalAlignment TrueValue { get; set; } = HorizontalAlignment.Left;

    public HorizontalAlignment FalseValue { get; set; } = HorizontalAlignment.Center;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            var alignment = flag ? TrueValue : FalseValue;

            if (targetType == typeof(object) || targetType == typeof(HorizontalAlignment) || targetType.IsAssignableFrom(typeof(HorizontalAlignment)))
            {
                return alignment;
            }

            if (targetType == typeof(string))
            {
                return alignment.ToString();
            }
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
