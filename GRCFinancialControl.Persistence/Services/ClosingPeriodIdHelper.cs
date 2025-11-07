using System;
using System.Globalization;
using System.Linq;

namespace GRCFinancialControl.Persistence.Services;

/// <summary>
/// Parsing utilities for closing period identifiers.
/// </summary>
internal static class ClosingPeriodIdHelper
{
    public static string? Normalize(string? closingPeriodId)
    {
        return string.IsNullOrWhiteSpace(closingPeriodId)
            ? null
            : closingPeriodId.Trim();
    }

    public static bool TryParsePeriodDate(string closingPeriodId, out DateTime parsedDate)
    {
        return DateTime.TryParse(
            closingPeriodId,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsedDate);
    }

    public static bool TryExtractDigits(string closingPeriodId, out int numericValue)
    {
        var digits = new string(closingPeriodId.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            numericValue = 0;
            return false;
        }

        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue);
    }
}
