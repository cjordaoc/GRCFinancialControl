using System.Collections.Generic;
using GRC.Shared.Core.Models.Lookups;

namespace GRC.Shared.Core.Models.Allocations
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
