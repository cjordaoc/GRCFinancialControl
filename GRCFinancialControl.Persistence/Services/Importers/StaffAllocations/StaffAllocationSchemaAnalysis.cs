using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

public enum StaffAllocationFixedColumn
{
    Gpn,
    UtilizationFytd,
    Rank,
    ResourceName,
    Office,
    Subdomain
}

public sealed record StaffAllocationColumnDefinition(int ColumnIndex, string HeaderText);

public sealed record StaffAllocationWeekColumn(int ColumnIndex, DateTime WeekStartMon, string HeaderText);

public sealed class StaffAllocationSchemaAnalysis
{
    public StaffAllocationSchemaAnalysis(
        int headerRowIndex,
        IReadOnlyDictionary<StaffAllocationFixedColumn, StaffAllocationColumnDefinition> fixedColumns,
        IReadOnlyList<StaffAllocationWeekColumn> weekColumns)
    {
        HeaderRowIndex = headerRowIndex;
        FixedColumns = fixedColumns ?? throw new ArgumentNullException(nameof(fixedColumns));
        WeekColumns = weekColumns ?? throw new ArgumentNullException(nameof(weekColumns));
    }

    public int HeaderRowIndex { get; }

    public IReadOnlyDictionary<StaffAllocationFixedColumn, StaffAllocationColumnDefinition> FixedColumns { get; }

    public IReadOnlyList<StaffAllocationWeekColumn> WeekColumns { get; }
}
