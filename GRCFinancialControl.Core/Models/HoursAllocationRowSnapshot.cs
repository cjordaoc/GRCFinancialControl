using System.Collections.Generic;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models
{
    public record HoursAllocationRowSnapshot(
        string RankName,
        decimal AdditionalHours,
        decimal IncurredHours,
        TrafficLightStatus Status,
        IReadOnlyList<HoursAllocationCellSnapshot> Cells);
}
