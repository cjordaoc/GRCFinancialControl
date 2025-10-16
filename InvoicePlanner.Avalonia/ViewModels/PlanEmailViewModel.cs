using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class PlanEmailViewModel : ObservableObject
{
    private readonly Action<PlanEmailViewModel> _removeAction;

    public PlanEmailViewModel(Action<PlanEmailViewModel> removeAction)
    {
        _removeAction = removeAction;
        RemoveCommand = new RelayCommand(() => _removeAction(this));
    }

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string email = string.Empty;

    public IRelayCommand RemoveCommand { get; }
}
