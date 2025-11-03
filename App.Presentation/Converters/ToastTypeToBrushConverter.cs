using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using App.Presentation.Services;

namespace App.Presentation.Converters;

public sealed class ToastTypeToBrushConverter : IValueConverter
{
    public IBrush? SuccessBrush { get; set; }

    public IBrush? WarningBrush { get; set; }

    public IBrush? ErrorBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not ToastType toastType)
        {
            return SuccessBrush;
        }

        return toastType switch
        {
            ToastType.Success => SuccessBrush,
            ToastType.Warning => WarningBrush,
            ToastType.Error => ErrorBrush,
            _ => SuccessBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
