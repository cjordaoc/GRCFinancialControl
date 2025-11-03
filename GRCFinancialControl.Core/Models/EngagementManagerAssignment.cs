using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents the assignment of a manager to an engagement for a defined period.
    /// </summary>
    public class EngagementManagerAssignment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public int ManagerId { get; set; }
        public Manager Manager { get; set; } = null!;
    }
}
