using System.Collections.Generic;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a client engagement.
    /// </summary>
    public class Engagement
    {
        public int Id { get; set; }
        public string EngagementId { get; set; } = string.Empty; // Business key from source files
        public string Description { get; set; } = string.Empty;
        public string CustomerKey { get; set; } = string.Empty;
        public decimal OpeningMargin { get; set; }
        public decimal OpeningValue { get; set; }
        public EngagementStatus Status { get; set; }
        public double TotalPlannedHours { get; set; }
        public decimal InitialHoursBudget { get; set; }
        public decimal ActualHours { get; set; }

        public ICollection<EngagementPapd> EngagementPapds { get; set; } = new List<EngagementPapd>();
        public ICollection<EngagementRankBudget> RankBudgets { get; set; } = new List<EngagementRankBudget>();
    }
}