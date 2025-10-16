
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InvoicePlanner.Avalonia.Resources;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceSummaryGroupViewModel : ObservableObject
{
    public InvoiceSummaryGroupViewModel(InvoiceSummaryGroup group)
    {
        if (group is null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        EngagementId = group.EngagementId;
        EngagementName = group.EngagementName;
        CustomerName = group.CustomerName;
        CustomerCode = group.CustomerCode;
        TotalAmount = group.TotalAmount;
        TotalPercentage = group.TotalPercentage;
        PlannedCount = group.PlannedCount;
        RequestedCount = group.RequestedCount;
        ClosedCount = group.ClosedCount;
        CanceledCount = group.CanceledCount;
        EmittedCount = group.EmittedCount;
        ReissuedCount = group.ReissuedCount;

        Items = new ObservableCollection<InvoiceSummaryItemViewModel>(
            group.Items.Select(item => new InvoiceSummaryItemViewModel(item)));
    }

    public string EngagementId { get; }

    public string EngagementName { get; }

    public string? CustomerName { get; }

    public string? CustomerCode { get; }

    public decimal TotalAmount { get; }

    public decimal TotalPercentage { get; }

    public int PlannedCount { get; }

    public int RequestedCount { get; }

    public int ClosedCount { get; }

    public int CanceledCount { get; }

    public int EmittedCount { get; }

    public int ReissuedCount { get; }

    public ObservableCollection<InvoiceSummaryItemViewModel> Items { get; }

    public string TotalAmountDisplay => Strings.Format("SummaryGroupTotalAmountFormat", TotalAmount);

    public string TotalPercentageDisplay => Strings.Format("SummaryGroupTotalPercentageFormat", TotalPercentage);

    public string PlannedCountDisplay => Strings.Format("SummaryGroupPlannedCountFormat", PlannedCount);

    public string RequestedCountDisplay => Strings.Format("SummaryGroupRequestedCountFormat", RequestedCount);

    public string ClosedCountDisplay => Strings.Format("SummaryGroupClosedCountFormat", ClosedCount);

    public string CanceledCountDisplay => Strings.Format("SummaryGroupCanceledCountFormat", CanceledCount);

    public string EmittedCountDisplay => Strings.Format("SummaryGroupEmittedCountFormat", EmittedCount);

    public string ReissuedCountDisplay => Strings.Format("SummaryGroupReissuedCountFormat", ReissuedCount);
}
