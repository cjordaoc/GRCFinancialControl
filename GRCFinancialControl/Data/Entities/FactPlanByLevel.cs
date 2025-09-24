using System;

namespace GRCFinancialControl.Data;

public class FactPlanByLevel
{
    public long PlanId { get; set; }

    public DateTime LoadUtc { get; set; }

    public long SourceSystemId { get; set; }

    public long MeasurementPeriodId { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public long LevelId { get; set; }

    public decimal PlannedHours { get; set; }

    public decimal? PlannedRate { get; set; }

    public DateTime CreatedUtc { get; set; }
}
