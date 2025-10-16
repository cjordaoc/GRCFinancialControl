using System;

namespace Invoices.Core.Models;

public class InvoiceSummaryResult
{
    public IReadOnlyList<InvoiceSummaryGroup> Groups { get; set; } = Array.Empty<InvoiceSummaryGroup>();

    public decimal TotalAmount { get; set; }

    public decimal TotalPercentage { get; set; }
}
