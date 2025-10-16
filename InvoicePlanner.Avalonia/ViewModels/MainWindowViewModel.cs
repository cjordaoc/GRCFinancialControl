using CommunityToolkit.Mvvm.ComponentModel;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string title = Strings.Get("AppTitle");

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    public MainWindowViewModel(HomeViewModel homeViewModel)
    {
        currentViewModel = homeViewModel;
    }
}
