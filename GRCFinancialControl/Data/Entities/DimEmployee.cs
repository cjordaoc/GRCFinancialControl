using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Data;

public class DimEmployee
{
    public long EmployeeId { get; set; }

    public string? EmployeeCode { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public ICollection<MapEmployeeAlias> Aliases { get; set; } = new List<MapEmployeeAlias>();
}
