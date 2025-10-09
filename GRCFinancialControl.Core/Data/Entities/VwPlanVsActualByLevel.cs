namespace GRCFinancialControl.Data;

public class VwPlanVsActualByLevel
{
    public string EngagementId { get; set; } = string.Empty;

    public long LevelId { get; set; }

    public decimal? PlannedHours { get; set; }

    public decimal? ActualHours { get; set; }
}
