using System.Collections.Generic;

namespace GRCFinancialControl.Data;

public class DimSourceSystem
{
    public long SourceSystemId { get; set; }

    public string SystemCode { get; set; } = string.Empty;

    public string SystemName { get; set; } = string.Empty;

    public ICollection<MapEmployeeAlias> EmployeeAliases { get; set; } = new List<MapEmployeeAlias>();

    public ICollection<MapLevelAlias> LevelAliases { get; set; } = new List<MapLevelAlias>();

    public ICollection<MapEmployeeCode> EmployeeCodes { get; set; } = new List<MapEmployeeCode>();
}
