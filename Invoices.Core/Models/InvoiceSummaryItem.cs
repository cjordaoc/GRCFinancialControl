using System;
using Invoices.Core.Enums;

namespace Invoices.Core.Models;

public class InvoiceSummaryItem
{
    public int ItemId { get; set; }

    public int PlanId { get; set; }

    public int Sequence { get; set; }

    public InvoicePlanType PlanType { get; set; }

    public InvoiceItemStatus Status { get; set; }

    public decimal Percentage { get; set; }

    public decimal Amount { get; set; }

    public DateTime? EmissionDate { get; set; }

    public DateTime? DueDate { get; set; }

    public string? RitmNumber { get; set; }

    public string? BzCode { get; set; }

    public DateTime? RequestDate { get; set; }

    public DateTime? EmittedAt { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CancelReason { get; set; }

    public decimal? BaseValue { get; set; }
}
