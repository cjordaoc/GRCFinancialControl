using System;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed class RequestConfirmationDialogViewModel : ViewModelBase
{
    public RequestConfirmationDialogViewModel(RequestConfirmationViewModel request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public RequestConfirmationViewModel Request { get; }
}
