using System;

namespace Invoices.Core.Models;

public class InvoiceReissueRequest
{
    public int ItemId { get; set; }

    public string CancelReason { get; set; } = string.Empty;

    public DateTime? ReplacementEmissionDate { get; set; }

    public DateTime? ReplacementDueDate { get; set; }
}
