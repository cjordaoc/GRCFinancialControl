using System;
using Invoices.Core.Enums;

namespace Invoices.Core.Models;

public class InvoiceSummaryFilter
{
    public string? EngagementId { get; set; }

    public int? CustomerId { get; set; }

    public IReadOnlyCollection<InvoiceItemStatus> Statuses { get; init; } = Array.Empty<InvoiceItemStatus>();
}
