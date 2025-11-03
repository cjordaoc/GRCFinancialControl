using System.Collections.Generic;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a Partner, Associate Partner, or Director (PAPD).
    /// </summary>
    public class Papd
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PapdLevel Level { get; set; } // e.g., "Partner", "Director"
        public string? WindowsLogin { get; set; }
        public ICollection<Engagement> Engagements { get; set; } = new List<Engagement>();
    }
}