using Invoices.Core.Enums;
using Invoices.Core.Payments;

namespace Invoices.Core.Models;

public class InvoiceItem
{
    public int Id { get; set; }

    public int PlanId { get; set; }

    public int SeqNo { get; set; }

    public decimal Percentage { get; set; }

    public decimal Amount { get; set; }

    public DateTime? EmissionDate { get; set; }

    public DateTime? DueDate { get; set; }

    public string PayerCnpj { get; set; } = string.Empty;

    public string? PoNumber { get; set; }

    public string? FrsNumber { get; set; }

    public string? CustomerTicket { get; set; }

    public string? AdditionalInfo { get; set; }

    public string? DeliveryDescription { get; set; }

    public InvoiceItemStatus Status { get; set; } = InvoiceItemStatus.Planned;

    public string? RitmNumber { get; set; }

    public string? CoeResponsible { get; set; }

    public DateTime? RequestDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string PaymentTypeCode { get; set; } = PaymentTypeCatalog.TransferenciaBancariaCode;

    public InvoicePlan? Plan { get; set; }

    public ICollection<MailOutbox> OutboxEntries { get; } = new List<MailOutbox>();

    public ICollection<InvoiceEmission> Emissions { get; } = new List<InvoiceEmission>();
}
