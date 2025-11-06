using System;

namespace Invoices.Core.Utilities;

public static class BusinessDayCalculator
{
    public static DateTime AdjustToNextBusinessDay(DateTime date)
    {
        var adjusted = date;

        while (!IsBusinessDay(adjusted))
        {
            adjusted = adjusted.AddDays(1);
        }

        return adjusted;
    }

    public static DateTime AdjustToNextMonday(DateTime date)
    {
        var adjusted = date;

        if (adjusted.DayOfWeek == DayOfWeek.Monday)
        {
            return adjusted;
        }

        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)adjusted.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        return adjusted.AddDays(daysUntilMonday);
    }

    public static bool IsBusinessDay(DateTime date)
    {
        return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }
}
