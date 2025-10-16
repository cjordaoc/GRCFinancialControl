using System;

namespace Invoices.Core.Models;

public class InvoiceSummaryGroup
{
    public string EngagementId { get; set; } = string.Empty;

    public string EngagementName { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public string? CustomerCode { get; set; }

    public int? CustomerId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal TotalPercentage { get; set; }

    public int PlannedCount { get; set; }

    public int RequestedCount { get; set; }

    public int ClosedCount { get; set; }

    public int CanceledCount { get; set; }

    public int EmittedCount { get; set; }

    public int ReissuedCount { get; set; }

    public IReadOnlyList<InvoiceSummaryItem> Items { get; set; } = Array.Empty<InvoiceSummaryItem>();
}
