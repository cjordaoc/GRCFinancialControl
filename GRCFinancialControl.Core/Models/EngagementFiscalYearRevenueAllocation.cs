using System;

namespace GRCFinancialControl.Core.Models
{
    public class EngagementFiscalYearRevenueAllocation
    {
        public EngagementFiscalYearRevenueAllocation()
        {
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = null!;
        public decimal ToGoValue { get; set; }
        public decimal ToDateValue { get; set; }
        public DateTime? LastUpdateDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
