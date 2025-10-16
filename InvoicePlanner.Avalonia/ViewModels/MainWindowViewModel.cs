using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _planEditor;
    private readonly RequestConfirmationViewModel _requestConfirmation;
    private readonly EmissionConfirmationViewModel _emissionConfirmation;

    [ObservableProperty]
    private string title = Strings.Get("AppTitle");

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    public IReadOnlyList<NavigationItemViewModel> MenuItems { get; }

    public MainWindowViewModel(
        PlanEditorViewModel planEditor,
        RequestConfirmationViewModel requestConfirmation,
        EmissionConfirmationViewModel emissionConfirmation)
    {
        _planEditor = planEditor;
        _requestConfirmation = requestConfirmation;
        _emissionConfirmation = emissionConfirmation;

        var items = new List<NavigationItemViewModel>
        {
            CreateNavigationItem(Strings.Get("NavInvoicePlan"), _planEditor),
            CreateNavigationItem(Strings.Get("NavConfirmRequest"), _requestConfirmation),
            CreateNavigationItem(Strings.Get("NavConfirmEmissions"), _emissionConfirmation)
        };

        MenuItems = items;
        currentViewModel = _planEditor;
        Activate(_planEditor, items.First());
    }

    private NavigationItemViewModel CreateNavigationItem(string title, ViewModelBase viewModel)
    {
        NavigationItemViewModel? item = null;
        item = new NavigationItemViewModel(title, selected => Activate(viewModel, selected));
        return item;
    }

    private void Activate(ViewModelBase viewModel, NavigationItemViewModel selectedItem)
    {
        CurrentViewModel = viewModel;
        foreach (var item in MenuItems)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }
    }

}
