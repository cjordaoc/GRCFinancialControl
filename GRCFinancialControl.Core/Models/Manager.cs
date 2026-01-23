using System;
using System.Collections.Generic;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models
{
    public class Manager
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public ManagerPosition Position { get; set; }
        public string? WindowsLogin { get; set; }
        
        /// <summary>
        /// Legacy GUI identifier (text name). Use GuiCode for the numeric identifier.
        /// </summary>
        public string? EngagementManagerGui { get; set; }
        
        /// <summary>
        /// Unique numeric GUI code from the spreadsheet (column BA).
        /// This is the new primary identifier for matching managers.
        /// </summary>
        public string? GuiCode { get; set; }
        
        public ICollection<EngagementManagerAssignment> EngagementAssignments { get; } = new List<EngagementManagerAssignment>();
    }
}

