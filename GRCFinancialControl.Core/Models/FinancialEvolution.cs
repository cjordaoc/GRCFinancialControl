namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a financial snapshot for an engagement at a specific closing period.
    /// Each snapshot captures point-in-time metrics across four categories:
    /// Hours, Revenue, Margin, and Expenses.
    /// 
    /// Design: Budget values (BudgetHours, BudgetMargin, ExpenseBudget) remain constant
    /// across all snapshots for the same engagement. ETD (Estimate To Date) values reflect
    /// the most recent actuals, while FYTD (Fiscal Year To Date) values accumulate within
    /// the fiscal year.
    /// </summary>
    public class FinancialEvolution
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Identifier of the closing period when this snapshot was captured.
        /// Can be either a numeric ID (resolved to ClosingPeriod.Id) or a period name.
        /// </summary>
        public string ClosingPeriodId { get; set; } = string.Empty;
        
        public int EngagementId { get; set; }
        
        // Hours Metrics
        
        /// <summary>
        /// Original budget hours allocated to the engagement (baseline, constant across snapshots).
        /// Maps from Excel "Original Budget Hours" column.
        /// </summary>
        public decimal? BudgetHours { get; set; }
        
        /// <summary>
        /// Estimate To Date (ETD) hours that have been charged to the engagement.
        /// Maps from Excel "Charged Hours ETD" column.
        /// </summary>
        public decimal? ChargedHours { get; set; }
        
        /// <summary>
        /// Fiscal Year To Date (FYTD) total hours accumulated within the current fiscal year.
        /// Maps from Excel "Charged Hours FYTD" column.
        /// </summary>
        public decimal? FYTDHours { get; set; }
        
        /// <summary>
        /// Extra hours allocated beyond the original budget.
        /// Reserved for future Hours Allocation View feature.
        /// </summary>
        public decimal? AdditionalHours { get; set; }
        
        // Revenue Metrics
        
        /// <summary>
        /// Total engagement revenue value (TER - Total Estimated Revenue).
        /// Maps from Excel "Ter Mercury Projected" column.
        /// </summary>
        public decimal? ValueData { get; set; }
        
        /// <summary>
        /// Remaining revenue to be realized (from backlog calculation).
        /// Calculated from Excel "FYTG Backlog" column.
        /// </summary>
        public decimal? RevenueToGoValue { get; set; }
        
        /// <summary>
        /// Revenue already realized to date.
        /// Calculated as: ValueToAllocate − CurrentBacklog − FutureBacklog.
        /// </summary>
        public decimal? RevenueToDateValue { get; set; }
        
        // Margin Metrics
        
        /// <summary>
        /// Baseline margin percentage from original budget (constant across snapshots).
        /// Maps from Excel "Original Budget Margin %" column.
        /// </summary>
        public decimal? BudgetMargin { get; set; }
        
        /// <summary>
        /// Current margin percentage (Estimate To Date).
        /// Maps from Excel "Margin % ETD" column.
        /// </summary>
        public decimal? ToDateMargin { get; set; }
        
        /// <summary>
        /// Fiscal year cumulative margin percentage.
        /// Maps from Excel "Margin % FYTD" column.
        /// </summary>
        public decimal? FYTDMargin { get; set; }
        
        // Expense Metrics
        
        /// <summary>
        /// Original budgeted expenses (baseline, constant across snapshots).
        /// Maps from Excel "Original Budget Expenses" column.
        /// </summary>
        public decimal? ExpenseBudget { get; set; }
        
        /// <summary>
        /// Expenses incurred to date (ETD).
        /// Maps from Excel "Expenses ETD" column.
        /// </summary>
        public decimal? ExpensesToDate { get; set; }
        
        /// <summary>
        /// Fiscal year cumulative expenses.
        /// Maps from Excel "Expenses FYTD" column.
        /// </summary>
        public decimal? FYTDExpenses { get; set; }
        
        // Foreign Keys
        
        /// <summary>
        /// Links this snapshot to a fiscal year for revenue allocation tracking.
        /// </summary>
        public int? FiscalYearId { get; set; }

        // Navigation Properties
        public Engagement Engagement { get; set; } = null!;
        public FiscalYear? FiscalYear { get; set; }
    }
}
