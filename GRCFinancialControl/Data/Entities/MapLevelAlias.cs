using System;

namespace GRCFinancialControl.Data;

public class MapLevelAlias
{
    public long LevelAliasId { get; set; }

    public long SourceSystemId { get; set; }

    public string RawLevel { get; set; } = string.Empty;

    public string NormalizedRaw { get; set; } = string.Empty;

    public long LevelId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DimSourceSystem? SourceSystem { get; set; }

    public DimLevel? Level { get; set; }
}
