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

        if (value is string text)
        {
            if (TryParseDateTimeOffset(text, culture, out var parsedOffset))
            {
                return Normalize(parsedOffset);
            }

            if (TryParseDateTime(text, culture, out var parsedDate))
            {
                return CreateOffset(parsedDate);
            }
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

        if (value is string text)
        {
            if (TryParseDateTimeOffset(text, culture, out var parsedOffset))
            {
                return parsedOffset.Date;
            }

            if (TryParseDateTime(text, culture, out var parsedDate))
            {
                return parsedDate.Date;
            }
        }

        return null;
    }

    private static bool TryParseDateTimeOffset(string text, CultureInfo? culture, out DateTimeOffset result)
    {
        var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal;

        if (DateTimeOffset.TryParse(text, culture, styles, out result))
        {
            return true;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, styles, out result);
    }

    private static bool TryParseDateTime(string text, CultureInfo? culture, out DateTime result)
    {
        var styles = DateTimeStyles.AllowWhiteSpaces;

        if (DateTime.TryParse(text, culture, styles, out result))
        {
            return true;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, styles, out result);
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
