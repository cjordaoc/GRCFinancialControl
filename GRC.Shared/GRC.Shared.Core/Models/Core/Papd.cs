using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

using GRC.Shared.Core.Enums;
using GRC.Shared.Core.Models.Assignments;

namespace GRC.Shared.Core.Models.Core
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

        public ICollection<EngagementPapd> EngagementPapds { get; } = new List<EngagementPapd>();
    }
}
