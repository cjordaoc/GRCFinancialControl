namespace GRCFinancialControl.Core.Models.Reporting
{
    public class FinancialEvolutionPoint
    {
        public string ClosingPeriodId { get; set; } = string.Empty;

        public DateTime? ClosingPeriodDate { get; set; }

        public decimal? Hours { get; set; }

        public decimal? Revenue { get; set; }

        public decimal? Margin { get; set; }

        public decimal? Expenses { get; set; }
    }
}
