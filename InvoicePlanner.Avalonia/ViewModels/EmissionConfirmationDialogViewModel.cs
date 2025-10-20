using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class EmissionConfirmationDialogViewModel : ViewModelBase
{
    public EmissionConfirmationDialogViewModel(EmissionConfirmationViewModel emission)
    {
        Emission = emission ?? throw new ArgumentNullException(nameof(emission));
    }

    public EmissionConfirmationViewModel Emission { get; }

    public IRelayCommand SaveCommand => Emission.SavePlanDetailsCommand;
    public IRelayCommand CloseCommand => Emission.ClosePlanDetailsCommand;

    [ObservableProperty]
    private bool canSave;
}
