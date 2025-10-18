using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class PlanEmailViewModel : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string email = string.Empty;
}
