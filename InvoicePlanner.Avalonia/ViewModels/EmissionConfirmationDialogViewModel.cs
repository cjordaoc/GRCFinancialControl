using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class EmissionConfirmationDialogViewModel : ViewModelBase
{
    public EmissionConfirmationDialogViewModel(EmissionConfirmationViewModel emission)
    {
        Emission = emission ?? throw new ArgumentNullException(nameof(emission));
    }

    public EmissionConfirmationViewModel Emission { get; }

    [ObservableProperty]
    private bool canSave;
}
