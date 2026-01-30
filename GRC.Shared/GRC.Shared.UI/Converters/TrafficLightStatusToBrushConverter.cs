using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.UI.Converters;

/// <summary>
/// Converts TrafficLightStatus enum values to appropriate foreground/border color brushes.
/// Uses theme-aware colors: Green (success), Yellow (warning), Red (error).
/// </summary>
public sealed class TrafficLightStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string statusStr)
        {
            if (Enum.TryParse<TrafficLightStatus>(statusStr, ignoreCase: true, out var status))
            {
                return status switch
                {
                    TrafficLightStatus.Green => new SolidColorBrush(Color.Parse("#28a745")),
                    TrafficLightStatus.Yellow => new SolidColorBrush(Color.Parse("#ffc107")),
                    TrafficLightStatus.Red => new SolidColorBrush(Color.Parse("#dc3545")),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts TrafficLightStatus enum values to background brush (lighter shade for readability).
/// </summary>
public sealed class TrafficLightStatusToLightBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string statusStr)
        {
            if (Enum.TryParse<TrafficLightStatus>(statusStr, ignoreCase: true, out var status))
            {
                return status switch
                {
                    TrafficLightStatus.Green => new SolidColorBrush(Color.Parse("#d4edda")),
                    TrafficLightStatus.Yellow => new SolidColorBrush(Color.Parse("#fff3cd")),
                    TrafficLightStatus.Red => new SolidColorBrush(Color.Parse("#f8d7da")),
                    _ => new SolidColorBrush(Colors.White)
                };
            }
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
