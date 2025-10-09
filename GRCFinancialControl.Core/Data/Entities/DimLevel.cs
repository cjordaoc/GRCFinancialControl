using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Data;

public class DimLevel
{
    public long LevelId { get; set; }

    public string LevelCode { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public ushort LevelOrder { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public ICollection<MapLevelAlias> Aliases { get; set; } = new List<MapLevelAlias>();
}
