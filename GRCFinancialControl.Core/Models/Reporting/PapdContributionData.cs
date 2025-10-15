namespace GRCFinancialControl.Core.Models.Reporting
{
    public class PapdContributionData
    {
        public string PapdName { get; set; } = string.Empty;
        public decimal RevenueContribution { get; set; }
        public List<HoursWorked> HoursWorked { get; set; } = [];
    }

    public class HoursWorked
    {
        public string ClosingPeriodName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }
}