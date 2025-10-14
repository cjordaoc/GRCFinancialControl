using System;

namespace GRCFinancialControl.Avalonia.Utilities
{
    internal static class DateTimeOffsetHelper
    {
        public static DateTimeOffset? FromDate(DateTime? date)
        {
            if (!date.HasValue || date.Value == default)
            {
                return null;
            }

            var unspecified = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecified, TimeSpan.Zero);
        }

        public static DateTime? ToDate(DateTimeOffset? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.Date;
        }
    }
}
