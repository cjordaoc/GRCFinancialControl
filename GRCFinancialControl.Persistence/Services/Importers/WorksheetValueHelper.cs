using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GRCFinancialControl.Persistence.Services.Importers;

internal static class WorksheetValueHelper
{
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex EngagementCodeExtractionRegex = new(@"E-\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

    public static string? TryExtractEngagementCode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        string text = value is string s
            ? s
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = EngagementCodeExtractionRegex.Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    public static string GetString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    public static string NormalizeToUpperInvariant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToUpperInvariant();
    }
}
