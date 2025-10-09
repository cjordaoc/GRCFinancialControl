using System;

namespace GRCFinancialControl.Data;

public class MapEmployeeCode
{
    public long EmployeeCodeId { get; set; }

    public long SourceSystemId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public long EmployeeId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DimSourceSystem? SourceSystem { get; set; }

    public DimEmployee? Employee { get; set; }
}
