using System;

namespace GRCFinancialControl.Core.Models
{
    public class StaffAllocationForecast
    {
        public long Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = null!;
        public string RankName { get; set; } = string.Empty;
        public decimal ForecastHours { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
