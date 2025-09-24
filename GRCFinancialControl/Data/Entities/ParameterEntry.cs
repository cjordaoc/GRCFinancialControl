using System;

namespace GRCFinancialControl.Data;

public class ParameterEntry
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedUtc { get; set; }
}
