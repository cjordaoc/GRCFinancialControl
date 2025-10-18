using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class PlanEmailViewModel : ObservableObject
{
    private readonly Action<PlanEmailViewModel> _removeAction;
    private readonly Action<PlanEmailViewModel>? _changedAction;

    public PlanEmailViewModel(
        Action<PlanEmailViewModel> removeAction,
        Action<PlanEmailViewModel>? changedAction = null)
    {
        _removeAction = removeAction;
        _changedAction = changedAction;
        RemoveCommand = new RelayCommand(() => _removeAction(this));
    }

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string email = string.Empty;

    public IRelayCommand RemoveCommand { get; }

    partial void OnEmailChanged(string value)
    {
        _changedAction?.Invoke(this);
    }
}
