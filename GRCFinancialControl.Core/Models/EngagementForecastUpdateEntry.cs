namespace GRCFinancialControl.Core.Models
{
    public sealed record EngagementForecastUpdateEntry(
        int FiscalYearId,
        string Rank,
        decimal ForecastHours);
}
