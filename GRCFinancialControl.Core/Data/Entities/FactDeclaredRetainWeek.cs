using System;

namespace GRCFinancialControl.Data;

public class FactDeclaredRetainWeek
{
    public long RetainId { get; set; }

    public long SourceSystemId { get; set; }

    public long MeasurementPeriodId { get; set; }

    public DateOnly WeekStartDate { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public decimal DeclaredHours { get; set; }

    public DateTime LoadUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}
