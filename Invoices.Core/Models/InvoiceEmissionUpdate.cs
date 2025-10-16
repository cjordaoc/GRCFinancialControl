using System;

namespace Invoices.Core.Models;

public class InvoiceEmissionUpdate
{
    public int ItemId { get; set; }

    public string BzCode { get; set; } = string.Empty;

    public DateTime? EmittedAt { get; set; }
}
