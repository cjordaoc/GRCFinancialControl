using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class RequestConfirmationDialogViewModel : ViewModelBase
{
    public RequestConfirmationDialogViewModel(RequestConfirmationViewModel request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public RequestConfirmationViewModel Request { get; }

    [ObservableProperty]
    private bool canSave;
}
