using Avalonia.Data.Converters;
using Avalonia.Media;
using GRC.Shared.UI.Services;
using System;
using System.Globalization;

namespace GRC.Shared.UI.Converters;

/// <summary>
/// Converts ToastType enum values to brushes for toast notification styling.
/// Supports Success (green), Warning (orange), and Error (red) severity levels.
/// </summary>
public class ToastTypeToBrushConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the brush used for success toast notifications.
    /// </summary>
    public IBrush? SuccessBrush { get; set; }

    /// <summary>
    /// Gets or sets the brush used for warning toast notifications.
    /// </summary>
    public IBrush? WarningBrush { get; set; }

    /// <summary>
    /// Gets or sets the brush used for error toast notifications.
    /// </summary>
    public IBrush? ErrorBrush { get; set; }

    /// <summary>
    /// Converts a ToastType to the corresponding brush.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is ToastType toastType)
        {
            return toastType switch
            {
                ToastType.Success => SuccessBrush,
                ToastType.Warning => WarningBrush,
                ToastType.Error => ErrorBrush,
                _ => ErrorBrush
            };
        }

        return ErrorBrush;
    }

    /// <summary>
    /// Not implemented - conversion is one-way only.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}
