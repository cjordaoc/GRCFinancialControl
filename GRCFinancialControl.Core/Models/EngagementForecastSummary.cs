using System;

namespace GRCFinancialControl.Core.Models
{
    public sealed record EngagementForecastSummary(
        int EngagementId,
        string EngagementCode,
        string EngagementName,
        decimal InitialHoursBudget,
        decimal ActualHours,
        decimal ForecastHours,
        decimal RemainingHours,
        int FiscalYearCount,
        int RankCount,
        int RiskCount,
        int OverrunCount)
    {
        public decimal Utilization => InitialHoursBudget == 0m
            ? 0m
            : Math.Round((ActualHours + ForecastHours) / InitialHoursBudget, 4, MidpointRounding.AwayFromZero);

        public bool HasRisk => RiskCount > 0;

        public bool HasOverrun => OverrunCount > 0;

        public string Status => HasOverrun
            ? "Estouro"
            : HasRisk
                ? "Risco"
                : "OK";
    }
}
