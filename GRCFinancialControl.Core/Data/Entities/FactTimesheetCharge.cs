using System;

namespace GRCFinancialControl.Data;

public class FactTimesheetCharge
{
    public long ChargeId { get; set; }

    public long SourceSystemId { get; set; }

    public long MeasurementPeriodId { get; set; }

    public DateOnly ChargeDate { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public decimal HoursCharged { get; set; }

    public decimal? CostAmount { get; set; }

    public DateTime LoadUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}
