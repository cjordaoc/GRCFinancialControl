
using System;
using System.Collections.ObjectModel;
using System.Linq;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public class InvoiceSummaryGroupViewModel : ObservableObject
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

    public string TotalAmountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.AmountFormat", TotalAmount);

    public string TotalPercentageDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.PercentageFormat", TotalPercentage);

    public string PlannedCountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.PlannedCountFormat", PlannedCount);

    public string RequestedCountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.RequestedCountFormat", RequestedCount);

    public string ClosedCountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.ClosedCountFormat", ClosedCount);

    public string CanceledCountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.CanceledCountFormat", CanceledCount);

    public string EmittedCountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.EmittedCountFormat", EmittedCount);

    public string ReissuedCountDisplay => LocalizationRegistry.Format("InvoiceSummary.Group.ReissuedCountFormat", ReissuedCount);
}
