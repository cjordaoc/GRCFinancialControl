
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Enums;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceSummaryStatusOption : ObservableObject
{
    public InvoiceSummaryStatusOption(InvoiceItemStatus status, bool isSelected)
    {
        Status = status;
        this.isSelected = isSelected;
    }

    public InvoiceItemStatus Status { get; }

    public string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}
