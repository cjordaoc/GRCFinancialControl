using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a monthly or weekly closing period that can be associated with margin imports.
    /// </summary>
    public class ClosingPeriod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public ICollection<ActualsEntry> ActualsEntries { get; set; } = new List<ActualsEntry>();
        public ICollection<PlannedAllocation> PlannedAllocations { get; set; } = new List<PlannedAllocation>();
    }
}
