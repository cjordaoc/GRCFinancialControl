using System;

namespace Invoices.Core.Models;

public class InvoiceEmissionCancellation
{
    public int ItemId { get; set; }

    public string CancelReason { get; set; } = string.Empty;

    public DateTime? CanceledAt { get; set; }
}
