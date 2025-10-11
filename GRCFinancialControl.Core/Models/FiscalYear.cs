using System;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Defines a fiscal year period.
    /// </summary>
    public class FiscalYear
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., "FY2025"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}