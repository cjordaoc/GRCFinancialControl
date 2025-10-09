using System;

namespace GRCFinancialControl.Data;

public partial class DimFiscalYear
{
    public long FiscalYearId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
