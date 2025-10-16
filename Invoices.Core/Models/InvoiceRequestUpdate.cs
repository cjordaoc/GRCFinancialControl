using System;

namespace Invoices.Core.Models;

public class InvoiceRequestUpdate
{
    public int ItemId { get; set; }

    public string RitmNumber { get; set; } = string.Empty;

    public string CoeResponsible { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; }
        = DateTime.Today;
}
