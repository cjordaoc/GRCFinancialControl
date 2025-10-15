using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models
{
    public class Manager
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public ManagerPosition Position { get; set; }
        public ICollection<EngagementManagerAssignment> EngagementAssignments { get; set; } = [];
    }
}
