using System;

namespace GRCFinancialControl.Data;

public class VwChargesSum
{
    public string EngagementId { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public DateOnly ChargeDate { get; set; }

    public decimal? HoursCharged { get; set; }
}
