using System;

namespace GRCFinancialControl.Data;

public class DimEngagement
{
    public string EngagementId { get; set; } = string.Empty;

    public string EngagementTitle { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? EngagementPartner { get; set; }

    public string? EngagementManager { get; set; }

    public decimal OpeningMargin { get; set; }

    public double CurrentMargin { get; set; }

    public DateTime? LastMarginUpdateDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
