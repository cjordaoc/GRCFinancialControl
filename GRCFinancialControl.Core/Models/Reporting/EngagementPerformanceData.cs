namespace GRCFinancialControl.Core.Models.Reporting
{
    public class EngagementPerformanceData
    {
        public string EngagementId { get; set; } = string.Empty;
        public string EngagementDescription { get; set; } = string.Empty;
        public decimal InitialHoursBudget { get; set; }
        public decimal EstimatedToCompleteHours { get; set; }
        public List<RankBudget> RankBudgets { get; set; } = [];
    }

    public class RankBudget
    {
        public string RankName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }
}