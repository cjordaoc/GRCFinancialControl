using System;

namespace GRCFinancialControl.Common
{
    public static class WeekHelper
    {
        public static DateOnly ToWeekStart(DateOnly date)
        {
            var day = (int)date.DayOfWeek;
            var offset = day == 0 ? 6 : day - 1;
            return date.AddDays(-offset);
        }

        public static DateOnly ToWeekEnd(DateOnly date)
        {
            return ToWeekStart(date).AddDays(6);
        }
    }
}
