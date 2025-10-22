using System;

namespace GRCFinancialControl.Core.Models
{
    public class EngagementRankBudget
    {
        public long Id { get; set; }
        public int EngagementId { get; set; }
        public int FiscalYearId { get; set; }
        public string RankName { get; set; } = string.Empty;
        public decimal BudgetHours { get; set; }
        public decimal ConsumedHours { get; set; }
        public decimal AdditionalHours { get; set; }
        public decimal IncurredHours { get; set; }
        public decimal RemainingHours { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public Engagement? Engagement { get; set; }
        public FiscalYear? FiscalYear { get; set; }
    }
}
