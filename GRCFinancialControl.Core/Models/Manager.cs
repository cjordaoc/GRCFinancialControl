using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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
        /// Engagement manager GUI identifier stored in column EngagementManagerGui.
        /// </summary>
        [Column("EngagementManagerGui")]
        public string? EngagementManagerGui { get; set; }
        
        public ICollection<EngagementManagerAssignment> EngagementAssignments { get; } = new List<EngagementManagerAssignment>();
    }
}

