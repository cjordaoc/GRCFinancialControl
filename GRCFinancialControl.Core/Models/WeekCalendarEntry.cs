using System;

namespace GRCFinancialControl.Core.Models
{
    public class WeekCalendarEntry
    {
        public DateTime WeekStartMon { get; set; }
        public DateTime WeekEndFri { get; set; }
        public DateTime? RetainAnchorStart { get; set; }
        public int FiscalYear { get; set; }
        public int? ClosingPeriodId { get; set; }
        public ClosingPeriod? ClosingPeriod { get; set; }
        public byte WorkingDaysCount { get; set; }
        public bool IsHolidayWeek { get; set; }
        public int WeekSeqInFY { get; set; }
        public int? WeekSeqInCP { get; set; }
    }
}
