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
    }
}