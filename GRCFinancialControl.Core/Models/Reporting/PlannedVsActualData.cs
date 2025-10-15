namespace GRCFinancialControl.Core.Models.Reporting
{
    public class PlannedVsActualData
    {
        public string EngagementId { get; set; } = string.Empty;
        public string EngagementDescription { get; set; } = string.Empty;
        public string PapdName { get; set; } = string.Empty;
        public string FiscalYear { get; set; } = string.Empty;
        public decimal PlannedHours { get; set; }
        public decimal ActualHours { get; set; }
        public decimal Variance => PlannedHours - ActualHours;
    }
}