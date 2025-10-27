using System;

namespace Invoices.Core.Models;

public class InvoiceEmission
{
    public int Id { get; set; }

    public int InvoiceItemId { get; set; }

    public string BzCode { get; set; } = string.Empty;

    public DateTime EmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CancelReason { get; set; }

    public InvoiceItem? InvoiceItem { get; set; }
}
