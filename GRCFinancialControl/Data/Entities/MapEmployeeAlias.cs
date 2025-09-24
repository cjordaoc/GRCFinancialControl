using System;

namespace GRCFinancialControl.Data;

public class MapEmployeeAlias
{
    public long EmployeeAliasId { get; set; }

    public long SourceSystemId { get; set; }

    public string RawName { get; set; } = string.Empty;

    public string NormalizedRaw { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DimSourceSystem? SourceSystem { get; set; }

    public DimEmployee? Employee { get; set; }
}
