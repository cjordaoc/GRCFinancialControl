using System;

namespace GRCFinancialControl.Data;

public class MeasurementPeriod
{
    public long PeriodId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
