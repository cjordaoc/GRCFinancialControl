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

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime.Date, DateTimeKind.Unspecified), TimeSpan.Zero),
            _ => null
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.Date,
            DateTime dateTime => dateTime.Date,
            _ => null
        };
    }
}
