using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Defines a fiscal year period.
    /// </summary>
    public class FiscalYear
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal AreaSalesTarget { get; set; }
        public decimal AreaRevenueTarget { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedAt { get; set; }
        public string? LockedBy { get; set; }

        public ICollection<ClosingPeriod> ClosingPeriods { get; set; } = [];
        public ICollection<StaffAllocationForecast> Forecasts { get; set; } = [];
    }
}
