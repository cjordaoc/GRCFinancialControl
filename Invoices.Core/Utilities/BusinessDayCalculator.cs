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

    public static bool IsBusinessDay(DateTime date)
    {
        return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }
}
