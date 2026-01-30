using System.ComponentModel.DataAnnotations;
using GRC.Shared.Core.Models.Core;

namespace GRC.Shared.Core.Models.Financial
{
    /// <summary>
    /// Represents an additional sale associated with an engagement.
    /// </summary>
    public class EngagementAdditionalSale
    {
        public int Id { get; set; }

        public int EngagementId { get; set; }

        public Engagement? Engagement { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? OpportunityId { get; set; }

        public decimal Value { get; set; }
    }
}
