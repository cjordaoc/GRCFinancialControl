using System;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed class EmissionConfirmationDialogViewModel : ViewModelBase
{
    public EmissionConfirmationDialogViewModel(EmissionConfirmationViewModel emission)
    {
        Emission = emission ?? throw new ArgumentNullException(nameof(emission));
    }

    public EmissionConfirmationViewModel Emission { get; }
}
