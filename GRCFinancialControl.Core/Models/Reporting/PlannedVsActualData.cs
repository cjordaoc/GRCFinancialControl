namespace GRCFinancialControl.Core.Models.Reporting
{
    public class PlannedVsActualData
    {
        public string EngagementId { get; set; } = string.Empty;
        public string EngagementDescription { get; set; } = string.Empty;
        public string PapdName { get; set; } = string.Empty;
        public string FiscalYear { get; set; } = string.Empty;
        public double PlannedHours { get; set; }
        public double ActualHours { get; set; }
        public double Variance => PlannedHours - ActualHours;
    }
}