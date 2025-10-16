using System;

namespace Invoices.Core.Models;

public class InvoiceNotificationPreview
{
    public int InvoiceItemId { get; set; }

    public DateTime NotifyDate { get; set; }

    public int PlanId { get; set; }

    public string EngagementId { get; set; } = string.Empty;

    public int NumInvoices { get; set; }

    public int PaymentTermDays { get; set; }

    public int? EngagementIntId { get; set; }

    public string? EngagementDescription { get; set; }

    public string? CustomerName { get; set; }

    public int SeqNo { get; set; }

    public DateTime EmissionDate { get; set; }

    public DateTime ComputedDueDate { get; set; }

    public decimal Amount { get; set; }

    public string? CustomerFocalPointName { get; set; }

    public string? CustomerFocalPointEmail { get; set; }

    public string? ExtraEmails { get; set; }

    public string? ManagerEmails { get; set; }

    public string? ManagerNames { get; set; }

    public string? PoNumber { get; set; }

    public string? FrsNumber { get; set; }

    public string? RitmNumber { get; set; }
}
