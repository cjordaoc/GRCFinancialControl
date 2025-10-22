using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Avalonia.Converters
{
    public sealed class TrafficLightStatusToBrushConverter : IValueConverter
    {
        public static TrafficLightStatusToBrushConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not TrafficLightStatus status)
            {
                return AvaloniaProperty.UnsetValue;
            }

            var resourceKey = status switch
            {
                TrafficLightStatus.Red => "BrushError",
                TrafficLightStatus.Yellow => "BrushWarning",
                _ => "BrushSuccess"
            };

            var application = Application.Current;
            if (application is not null &&
                application.Resources.TryGetResource(resourceKey, application.ActualThemeVariant, out var brushResource) &&
                brushResource is IBrush brush)
            {
                return brush;
            }

            return Brushes.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
