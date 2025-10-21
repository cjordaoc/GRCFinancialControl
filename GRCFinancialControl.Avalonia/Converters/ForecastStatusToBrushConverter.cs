using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace GRCFinancialControl.Avalonia.Converters
{
    public sealed class ForecastStatusToBrushConverter : IValueConverter
    {
        public static readonly ForecastStatusToBrushConverter Instance = new();

        public ForecastStatusToBrushConverter()
        {
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value as string;

            return status switch
            {
                "Estouro" => ResolveBrush("BrushError", Brushes.Crimson),
                "Risco" => ResolveBrush("BrushWarning", Brushes.DarkGoldenrod),
                "OK" => ResolveBrush("BrushSuccess", Brushes.ForestGreen),
                _ => ResolveBrush("BrushOnSurface", Brushes.White)
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static IBrush ResolveBrush(string resourceKey, IBrush fallback)
        {
            var theme = Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;

            if (Application.Current?.Resources.TryGetResource(resourceKey, theme, out var resource) == true && resource is IBrush brush)
            {
                return brush;
            }

            return fallback;
        }
    }
}
