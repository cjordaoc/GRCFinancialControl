namespace GRCFinancialControl.Core.Models.Reporting
{
    public class BacklogData
    {
        public string EngagementId { get; set; } = string.Empty;
        public string EngagementDescription { get; set; } = string.Empty;
        public decimal BacklogHours { get; set; }
    }
}