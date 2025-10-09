using System;

namespace GRCFinancialControl.Data;

public class FactEtcSnapshot
{
    public long EtcId { get; set; }

    public string SnapshotLabel { get; set; } = string.Empty;

    public DateTime LoadUtc { get; set; }

    public long SourceSystemId { get; set; }

    public long MeasurementPeriodId { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public long? LevelId { get; set; }

    public decimal HoursIncurred { get; set; }

    public decimal EtcRemaining { get; set; }

    public DateTime CreatedUtc { get; set; }
}
