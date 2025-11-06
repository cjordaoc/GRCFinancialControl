using Invoices.Core.Enums;

namespace Invoices.Core.Models;

public class InvoicePlan
{
    public int Id { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public InvoicePlanType Type { get; set; }

    public int NumInvoices { get; set; }

    public int PaymentTermDays { get; set; }

    public string CustomerFocalPointName { get; set; } = string.Empty;

    public string CustomerFocalPointEmail { get; set; } = string.Empty;

    public string? CustomInstructions { get; set; }

    public string? AdditionalDetails { get; set; }

    public DateTime? FirstEmissionDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<InvoiceItem> Items { get; } = new List<InvoiceItem>();

    public ICollection<InvoicePlanEmail> AdditionalEmails { get; } = new List<InvoicePlanEmail>();
}
