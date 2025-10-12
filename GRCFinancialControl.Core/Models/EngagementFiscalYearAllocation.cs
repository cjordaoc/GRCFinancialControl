namespace GRCFinancialControl.Core.Models
{
    public class EngagementFiscalYearAllocation
    {
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; }
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; }
        public double Hours { get; set; }
    }
}