using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace App.Presentation.Converters;

public sealed class DateTimeOffsetToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        return NormalizeDate(value, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        return NormalizeDate(value, culture);
    }

    private static DateTime? NormalizeDate(object value, CultureInfo culture)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => DateTime.SpecifyKind(dateTimeOffset.Date, DateTimeKind.Unspecified),
            DateTime dateTime => DateTime.SpecifyKind(dateTime.Date, DateTimeKind.Unspecified),
            string text when DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
                => DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified),
            _ => null
        };
    }
}
