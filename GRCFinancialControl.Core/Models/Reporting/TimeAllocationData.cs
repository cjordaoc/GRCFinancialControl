namespace GRCFinancialControl.Core.Models.Reporting
{
    public class TimeAllocationData
    {
        public string ClosingPeriodName { get; set; } = string.Empty;
        public decimal PlannedHours { get; set; }
        public decimal ActualHours { get; set; }
    }
}