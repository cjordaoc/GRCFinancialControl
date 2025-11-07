using System;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a revenue allocation snapshot for an engagement at a specific closing period.
    /// Each snapshot captures the revenue allocation state (ToGo, ToDate) at that point in time,
    /// enabling historical tracking and audit trails.
    /// 
    /// Unique constraint: (EngagementId, FiscalYearId, ClosingPeriodId)
    /// </summary>
    public class EngagementFiscalYearRevenueAllocation
    {
        public EngagementFiscalYearRevenueAllocation()
        {
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public int Id { get; set; }
        
        /// <summary>
        /// The engagement this allocation belongs to.
        /// </summary>
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        
        /// <summary>
        /// The fiscal year this allocation applies to.
        /// </summary>
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = null!;
        
        /// <summary>
        /// The closing period when this snapshot was captured.
        /// This makes the allocation a historical snapshot rather than a mutable "current" value.
        /// </summary>
        public int ClosingPeriodId { get; set; }
        public ClosingPeriod ClosingPeriod { get; set; } = null!;
        
        /// <summary>
        /// Revenue remaining to be realized (To-Go).
        /// </summary>
        public decimal ToGoValue { get; set; }
        
        /// <summary>
        /// Revenue already realized to date.
        /// </summary>
        public decimal ToDateValue { get; set; }
        
        /// <summary>
        /// Total allocated revenue for this fiscal year.
        /// </summary>
        public decimal TotalValue => ToGoValue + ToDateValue;
        
        /// <summary>
        /// Timestamp when this allocation was last modified.
        /// </summary>
        public DateTime? LastUpdateDate { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
