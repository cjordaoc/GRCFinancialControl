using System;
using System.Globalization;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

internal static class StaffAllocationCellHelper
{
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
}
