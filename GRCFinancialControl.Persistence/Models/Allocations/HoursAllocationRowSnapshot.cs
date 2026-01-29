using System.Collections.Generic;

using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRCFinancialControl.Persistence.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.Core.Models.Allocations
{
    public record HoursAllocationRowSnapshot(
        string RankName,
        decimal AdditionalHours,
        decimal IncurredHours,
        TrafficLightStatus Status,
        IReadOnlyList<HoursAllocationCellSnapshot> Cells);
}
