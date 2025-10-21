using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    public sealed record ForecastAllocationRow(
        int EngagementId,
        string EngagementCode,
        string EngagementName,
        int FiscalYearId,
        string FiscalYearName,
        string Rank,
        decimal BudgetHours,
        decimal ActualsHours,
        decimal ForecastHours,
        decimal AvailableHours,
        decimal AvailableToActuals,
        string Status);

    public sealed record StaffAllocationForecastUpdateResult(
        int ProcessedRecords,
        int UpdatedEngagements,
        IReadOnlyCollection<string> MissingEngagements,
        IReadOnlyCollection<string> MissingBudgets,
        IReadOnlyCollection<string> UnknownRanks,
        IReadOnlyList<ForecastAllocationRow> Rows,
        int RiskCount,
        int OverrunCount);
}
