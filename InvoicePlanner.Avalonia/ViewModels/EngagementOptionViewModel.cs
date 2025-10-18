using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class EngagementOptionViewModel : ObservableObject
{
    public EngagementOptionViewModel()
    {
    }

    public EngagementOptionViewModel(EngagementLookup lookup)
    {
        Id = lookup.Id;
        EngagementId = lookup.EngagementId;
        Name = lookup.Name;
        CustomerName = lookup.CustomerName;
    }

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string engagementId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? customerName;
}
