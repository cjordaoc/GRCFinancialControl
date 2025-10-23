using System;

namespace GRCFinancialControl.Core.Models
{
    public class EngagementRankBudgetHistory
    {
        public int Id { get; set; }
        public string EngagementCode { get; set; } = string.Empty;
        public string RankCode { get; set; } = string.Empty;
        public int FiscalYearId { get; set; }
        public int ClosingPeriodId { get; set; }
        public decimal Hours { get; set; }
        public DateTime UploadedAt { get; set; }

        public ClosingPeriod? ClosingPeriod { get; set; }
        public FiscalYear? FiscalYear { get; set; }
    }
}
