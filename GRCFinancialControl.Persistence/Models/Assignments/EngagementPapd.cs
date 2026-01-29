using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRCFinancialControl.Persistence.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRCFinancialControl.Persistence.Models.Assignments
{
    /// <summary>
    /// Represents the assignment of a PAPD to an Engagement.
    /// </summary>
    public class EngagementPapd
    {
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public int PapdId { get; set; }
        public Papd Papd { get; set; } = null!;
    }
}
