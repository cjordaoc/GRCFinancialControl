using System;

namespace GRCFinancialControl.Core.Models
{
    public class FinancialEvolution
    {
        public int Id { get; set; }
        public string ClosingPeriodId { get; set; } = string.Empty;
        public string EngagementId { get; set; } = string.Empty;
        public decimal? HoursData { get; set; }
        public decimal? ValueData { get; set; }
        public decimal? MarginData { get; set; }
        public decimal? ExpenseData { get; set; }

        public Engagement? Engagement { get; set; }
    }
}
