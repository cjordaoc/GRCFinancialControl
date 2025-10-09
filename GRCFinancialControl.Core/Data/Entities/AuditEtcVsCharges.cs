using System;

namespace GRCFinancialControl.Data;

public class AuditEtcVsCharges
{
    public long AuditId { get; set; }

    public string SnapshotLabel { get; set; } = string.Empty;

    public long MeasurementPeriodId { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public DateOnly LastWeekEndDate { get; set; }

    public decimal EtcHoursIncurred { get; set; }

    public decimal ChargesSumHours { get; set; }

    public decimal DiffHours { get; set; }

    public DateTime CreatedUtc { get; set; }
}
