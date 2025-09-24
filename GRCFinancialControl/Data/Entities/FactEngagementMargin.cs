namespace GRCFinancialControl.Data;

public class FactEngagementMargin
{
    public long MeasurementPeriodId { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public decimal MarginValue { get; set; }
}
