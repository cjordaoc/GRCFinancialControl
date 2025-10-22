using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    public record HoursAllocationSnapshot(
        int EngagementId,
        string EngagementCode,
        string EngagementName,
        decimal TotalBudgetHours,
        decimal ActualHours,
        decimal ToBeConsumedHours,
        IReadOnlyList<FiscalYearAllocationInfo> FiscalYears,
        IReadOnlyList<RankOption> RankOptions,
        IReadOnlyList<HoursAllocationRowSnapshot> Rows);
}
