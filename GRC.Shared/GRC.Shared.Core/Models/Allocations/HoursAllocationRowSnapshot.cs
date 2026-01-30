using System.Collections.Generic;
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
