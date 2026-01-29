using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRCFinancialControl.Persistence.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.Core.Models.Allocations
{
    /// <summary>
    /// Represents the user's allocation of planned hours for an engagement into a fiscal year.
    /// </summary>
    public class PlannedAllocation
    {
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public int ClosingPeriodId { get; set; }
        public ClosingPeriod ClosingPeriod { get; set; } = null!;
        public decimal AllocatedHours { get; set; }
    }
}
