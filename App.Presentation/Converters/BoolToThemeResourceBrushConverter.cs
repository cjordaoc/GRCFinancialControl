using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace App.Presentation.Converters;

/// <summary>
/// Converts boolean values to theme-aware brushes via resource key lookup.
/// </summary>
public sealed class BoolToThemeResourceBrushConverter : IValueConverter
{
    public string TrueResourceKey { get; set; } = "ThemeErrorBrush";

    public string FalseResourceKey { get; set; } = "ThemeForegroundBrush";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value is bool flag && flag ? TrueResourceKey : FalseResourceKey;

        var application = Application.Current;
        if (application?.Resources.TryGetResource(resourceKey, application.ActualThemeVariant, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
