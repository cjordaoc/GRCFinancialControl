
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using InvoicePlanner.Avalonia.Resources;
using Invoices.Core.Enums;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceSummaryItemViewModel : ObservableObject
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
            InvoiceItemStatus.Planned => Strings.Get("StatusPlanned"),
            InvoiceItemStatus.Requested => Strings.Get("StatusRequested"),
            InvoiceItemStatus.Emitted => Strings.Get("StatusEmitted"),
            InvoiceItemStatus.Closed => Strings.Get("StatusClosed"),
            InvoiceItemStatus.Canceled => Strings.Get("StatusCanceled"),
            InvoiceItemStatus.Reissued => Strings.Get("StatusReissued"),
            _ => status.ToString(),
        };
    }
}
