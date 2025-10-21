using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GRCFinancialControl.Persistence.Services.Importers;

internal static class WorksheetValueHelper
{
    private static readonly Regex MultiWhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    public static bool IsBlank(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    public static string GetDisplayText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DBNull => string.Empty,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    public static DateTime NormalizeWeekStart(DateTime date)
    {
        var truncated = date.Date;
        var offset = ((int)truncated.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return truncated.AddDays(-offset);
    }

    public static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return MultiWhitespaceRegex.Replace(value.Trim(), " ");
    }
}
