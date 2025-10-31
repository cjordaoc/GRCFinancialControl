using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using App.Presentation.Services;

namespace App.Presentation.Converters;

/// <summary>
/// Formats a monetary value using the provided currency code and the shared currency helper.
/// </summary>
public sealed class CurrencyDisplayConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1)
        {
            return string.Empty;
        }

        if (values[0] is not IConvertible)
        {
            return string.Empty;
        }

        var amount = System.Convert.ToDecimal(values[0], CultureInfo.InvariantCulture);
        string? currencyCode = null;

        if (values.Count > 1 && values[1] is string code)
        {
            currencyCode = code;
        }

        return CurrencyDisplayHelper.Format(amount, currencyCode);
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
