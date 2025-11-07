namespace GRCFinancialControl.Core.Models
{
    public class FinancialEvolution
    {
        public int Id { get; set; }
        public string ClosingPeriodId { get; set; } = string.Empty;
        public int EngagementId { get; set; }
        
        // Hours Metrics
        public decimal? BudgetHours { get; set; }
        public decimal? ChargedHours { get; set; }
        public decimal? FYTDHours { get; set; }
        public decimal? AdditionalHours { get; set; }
        
        // Revenue Metrics
        public decimal? ValueData { get; set; }
        public decimal? RevenueToGoValue { get; set; }
        public decimal? RevenueToDateValue { get; set; }
        
        // Margin Metrics
        public decimal? BudgetMargin { get; set; }
        public decimal? ToDateMargin { get; set; }
        public decimal? FYTDMargin { get; set; }
        
        // Expense Metrics
        public decimal? ExpenseBudget { get; set; }
        public decimal? ExpensesToDate { get; set; }
        public decimal? FYTDExpenses { get; set; }
        
        // Foreign Keys
        public int? FiscalYearId { get; set; }

        // Navigation Properties
        public Engagement Engagement { get; set; } = null!;
        public FiscalYear? FiscalYear { get; set; }
    }
}
