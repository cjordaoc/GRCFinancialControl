namespace GRCFinancialControl.Core.Models
{
    public class EngagementRankBudget
    {
        public long Id { get; set; }
        public int EngagementId { get; set; }
        public string RankName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public Engagement? Engagement { get; set; }
    }
}
