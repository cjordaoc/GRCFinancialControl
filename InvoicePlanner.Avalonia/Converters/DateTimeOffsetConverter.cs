using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace InvoicePlanner.Avalonia.Converters;

public sealed class DateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return Normalize(dateTimeOffset);
        }

        if (value is DateTime dateTime)
        {
            return CreateOffset(dateTime);
        }

        if (value is string text && DateTimeOffset.TryParse(text, culture, DateTimeStyles.AssumeUniversal, out var parsedOffset))
        {
            return Normalize(parsedOffset);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.Date;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.Date;
        }

        if (value is string text && DateTimeOffset.TryParse(text, culture, DateTimeStyles.AssumeUniversal, out var parsedOffset))
        {
            return parsedOffset.Date;
        }

        return null;
    }

    private static DateTimeOffset Normalize(DateTimeOffset dateTimeOffset)
    {
        return new DateTimeOffset(dateTimeOffset.Date, TimeSpan.Zero);
    }

    private static DateTimeOffset CreateOffset(DateTime dateTime)
    {
        var unspecified = DateTime.SpecifyKind(dateTime.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeSpan.Zero);
    }
}
