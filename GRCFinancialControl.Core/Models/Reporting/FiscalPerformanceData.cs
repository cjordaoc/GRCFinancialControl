namespace GRCFinancialControl.Core.Models.Reporting
{
    public class FiscalPerformanceData
    {
        public int FiscalYearId { get; set; }
        public string? FiscalYearName { get; set; }
        public decimal AreaSalesTarget { get; set; }
        public decimal AreaRevenueTarget { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalEstimatedToCompleteHours { get; set; }
        public decimal TotalActualHours { get; set; }
        public List<PapdContribution> PapdContributions { get; set; } = [];
    }

    public class PapdContribution
    {
        public string PapdName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}