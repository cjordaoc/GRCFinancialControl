using System;
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
        
        /// <summary>
        /// Legacy GUI identifier (text name). Use GuiCode for the numeric identifier.
        /// </summary>
        public string? EngagementPapdGui { get; set; }
        
        /// <summary>
        /// Unique numeric GUI code from the spreadsheet (column AN).
        /// This is the new primary identifier for matching PAPDs.
        /// </summary>
        public string? GuiCode { get; set; }
        
        public ICollection<EngagementPapd> EngagementPapds { get; set; } = new List<EngagementPapd>();
    }
}
