using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Enums;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoicePlanSummaryViewModel : ObservableObject
{
    [ObservableProperty]
    private int plannedItemCount;

    [ObservableProperty]
    private int requestedItemCount;

    [ObservableProperty]
    private int emittedItemCount;

    [ObservableProperty]
    private int closedItemCount;

    [ObservableProperty]
    private int canceledItemCount;

    public int Id { get; }

    public string EngagementId { get; }

    public string EngagementDescription { get; }

    public string? CustomerName { get; }

    public InvoicePlanType Type { get; }

    public DateTime CreatedAt { get; }

    public DateTime? FirstEmissionDate { get; }

    public string TypeName => Type.ToString();

    public bool HasPendingRequests => PlannedItemCount > 0;

    public bool HasPendingEmissions => RequestedItemCount > 0 || EmittedItemCount > 0;

    private InvoicePlanSummaryViewModel(
        int id,
        string engagementId,
        string engagementDescription,
        string? customerName,
        InvoicePlanType type,
        DateTime createdAt,
        DateTime? firstEmissionDate,
        int planned,
        int requested,
        int emitted,
        int closed,
        int canceled)
    {
        Id = id;
        EngagementId = engagementId;
        EngagementDescription = engagementDescription;
        CustomerName = customerName;
        Type = type;
        CreatedAt = createdAt;
        FirstEmissionDate = firstEmissionDate;
        plannedItemCount = planned;
        requestedItemCount = requested;
        emittedItemCount = emitted;
        closedItemCount = closed;
        canceledItemCount = canceled;
    }

    public static InvoicePlanSummaryViewModel FromSummary(InvoicePlanSummary summary)
    {
        if (summary is null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        return new InvoicePlanSummaryViewModel(
            summary.Id,
            summary.EngagementId,
            summary.EngagementDescription,
            summary.CustomerName,
            summary.Type,
            summary.CreatedAt,
            summary.FirstEmissionDate,
            summary.PlannedItemCount,
            summary.RequestedItemCount,
            summary.EmittedItemCount,
            summary.ClosedItemCount,
            summary.CanceledItemCount);
    }

    public void UpdateCounts(int planned, int requested, int emitted, int closed, int canceled)
    {
        PlannedItemCount = planned;
        RequestedItemCount = requested;
        EmittedItemCount = emitted;
        ClosedItemCount = closed;
        CanceledItemCount = canceled;
    }

    partial void OnPlannedItemCountChanged(int value) => OnPropertyChanged(nameof(HasPendingRequests));

    partial void OnRequestedItemCountChanged(int value) => OnPropertyChanged(nameof(HasPendingEmissions));

    partial void OnEmittedItemCountChanged(int value) => OnPropertyChanged(nameof(HasPendingEmissions));
}
