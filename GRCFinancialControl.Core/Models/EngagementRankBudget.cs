using System;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a hours allocation snapshot for a specific rank at a specific closing period.
    /// Each snapshot captures the budget state (BudgetHours, ConsumedHours, AdditionalHours) 
    /// at that point in time, enabling historical tracking and audit trails.
    /// 
    /// Unique constraint: (EngagementId, FiscalYearId, RankName, ClosingPeriodId)
    /// </summary>
    public class EngagementRankBudget
    {
        public long Id { get; set; }
        
        /// <summary>
        /// The engagement this budget belongs to.
        /// </summary>
        public int EngagementId { get; set; }
        public Engagement? Engagement { get; set; }
        
        /// <summary>
        /// The fiscal year this budget applies to.
        /// </summary>
        public int FiscalYearId { get; set; }
        public FiscalYear? FiscalYear { get; set; }
        
        /// <summary>
        /// The closing period when this snapshot was captured.
        /// This makes the budget a historical snapshot rather than a mutable "current" value.
        /// </summary>
        public int ClosingPeriodId { get; set; }
        public ClosingPeriod? ClosingPeriod { get; set; }
        
        /// <summary>
        /// The rank (e.g., "Manager", "Senior") this budget entry applies to.
        /// </summary>
        public string RankName { get; set; } = string.Empty;
        
        /// <summary>
        /// Budgeted hours for this rank in this fiscal year.
        /// </summary>
        public decimal BudgetHours { get; set; }
        
        /// <summary>
        /// Hours already consumed/forecasted for this rank.
        /// </summary>
        public decimal ConsumedHours { get; set; }
        
        /// <summary>
        /// Additional hours allocated beyond the original budget.
        /// </summary>
        public decimal AdditionalHours { get; set; }
        
        /// <summary>
        /// Remaining hours calculated as: (BudgetHours + AdditionalHours) - (IncurredHours + ConsumedHours).
        /// </summary>
        public decimal RemainingHours { get; set; }
        
        /// <summary>
        /// Traffic light status: "Green", "Yellow", or "Red".
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
