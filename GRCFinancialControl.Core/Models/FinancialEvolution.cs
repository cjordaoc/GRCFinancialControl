namespace GRCFinancialControl.Core.Models
{
    public class FinancialEvolution
    {
        public int Id { get; set; }
        public string ClosingPeriodId { get; set; } = string.Empty;
        public int EngagementId { get; set; }
        public decimal? HoursData { get; set; }
        public decimal? ValueData { get; set; }
        public decimal? MarginData { get; set; }
        public decimal? ExpenseData { get; set; }
        public int? FiscalYearId { get; set; }
        public decimal? RevenueToGoValue { get; set; }
        public decimal? RevenueToDateValue { get; set; }

        public Engagement Engagement { get; set; } = null!;
        public FiscalYear? FiscalYear { get; set; }
    }
}
