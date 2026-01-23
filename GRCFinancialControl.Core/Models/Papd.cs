using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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
        
        /// <summary>
        /// Engagement partner GUI identifier stored in column EngagementPapdGui.
        /// </summary>
        [Column("EngagementPapdGui")]
        public string? EngagementPapdGui { get; set; }
        
        public ICollection<EngagementPapd> EngagementPapds { get; set; } = new List<EngagementPapd>();
    }
}
