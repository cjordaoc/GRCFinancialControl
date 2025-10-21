using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    public record HoursAllocationRowSnapshot(
        string RankName,
        IReadOnlyList<HoursAllocationCellSnapshot> Cells);
}
