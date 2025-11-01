
using System;
using App.Presentation.Localization;
using App.Presentation.Services;
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

    public string AmountDisplay => CurrencyDisplayHelper.Format(Amount, null);

    public string BaseValueDisplay => BaseValue.HasValue
        ? CurrencyDisplayHelper.Format(BaseValue.Value, null)
        : string.Empty;

    private static string GetStatusDisplay(InvoiceItemStatus status)
    {
        return status switch
        {
            InvoiceItemStatus.Planned => LocalizationRegistry.Get("INV_Invoice_Status_Planned"),
            InvoiceItemStatus.Requested => LocalizationRegistry.Get("INV_Invoice_Status_Requested"),
            InvoiceItemStatus.Emitted => LocalizationRegistry.Get("INV_Invoice_Status_Emitted"),
            InvoiceItemStatus.Closed => LocalizationRegistry.Get("INV_Invoice_Status_Closed"),
            InvoiceItemStatus.Canceled => LocalizationRegistry.Get("INV_Invoice_Status_Canceled"),
            InvoiceItemStatus.Reissued => LocalizationRegistry.Get("INV_Invoice_Status_Reissued"),
            _ => status.ToString(),
        };
    }
}
