using GRC.Shared.Core.Models.Core;

namespace GRC.Shared.Core.Models.Assignments
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
