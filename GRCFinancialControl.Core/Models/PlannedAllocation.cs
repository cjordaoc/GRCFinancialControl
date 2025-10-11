namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents the user's allocation of planned hours for an engagement into a fiscal year.
    /// </summary>
    public class PlannedAllocation
    {
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = null!;
        public double AllocatedHours { get; set; }
    }
}