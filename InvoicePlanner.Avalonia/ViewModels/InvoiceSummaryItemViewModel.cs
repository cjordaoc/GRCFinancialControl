
using System;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Enums;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public class InvoiceSummaryItemViewModel : ObservableObject
{
    public InvoiceSummaryItemViewModel(InvoiceSummaryItem item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        ItemId = item.ItemId;
        PlanId = item.PlanId;
        Sequence = item.Sequence;
        Status = item.Status;
        StatusDisplay = GetStatusDisplay(item.Status);
        PlanType = item.PlanType;
        Percentage = item.Percentage;
        Amount = item.Amount;
        BaseValue = item.BaseValue;
        EmissionDate = item.EmissionDate;
        DueDate = item.DueDate;
        RequestDate = item.RequestDate;
        EmittedAt = item.EmittedAt;
        BzCode = item.BzCode;
        RitmNumber = item.RitmNumber;
        CanceledAt = item.CanceledAt;
        CancelReason = item.CancelReason;
    }

    public int ItemId { get; }

    public int PlanId { get; }

    public int Sequence { get; }

    public InvoiceItemStatus Status { get; }

    public string StatusDisplay { get; }

    public InvoicePlanType PlanType { get; }

    public decimal Percentage { get; }

    public decimal Amount { get; }

    public decimal? BaseValue { get; }

    public DateTime? EmissionDate { get; }

    public DateTime? DueDate { get; }

    public DateTime? RequestDate { get; }

    public DateTime? EmittedAt { get; }

    public string? BzCode { get; }

    public string? RitmNumber { get; }

    public DateTime? CanceledAt { get; }

    public string? CancelReason { get; }

    private static string GetStatusDisplay(InvoiceItemStatus status)
    {
        return status switch
        {
            InvoiceItemStatus.Planned => LocalizationRegistry.Get("Invoice.Status.Planned"),
            InvoiceItemStatus.Requested => LocalizationRegistry.Get("Invoice.Status.Requested"),
            InvoiceItemStatus.Emitted => LocalizationRegistry.Get("Invoice.Status.Emitted"),
            InvoiceItemStatus.Closed => LocalizationRegistry.Get("Invoice.Status.Closed"),
            InvoiceItemStatus.Canceled => LocalizationRegistry.Get("Invoice.Status.Canceled"),
            InvoiceItemStatus.Reissued => LocalizationRegistry.Get("Invoice.Status.Reissued"),
            _ => status.ToString(),
        };
    }
}
