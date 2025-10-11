using System.Collections.Generic;

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
        public string Status { get; set; } = string.Empty;
        public double TotalPlannedHours { get; set; }

        public ICollection<EngagementPapd> EngagementPapds { get; set; } = new List<EngagementPapd>();
    }
}