using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class RequestConfirmationDialogViewModel : ViewModelBase
{
    public RequestConfirmationDialogViewModel(RequestConfirmationViewModel request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        request.LoadPlanCommand.Execute(null);
    }

    public RequestConfirmationViewModel Request { get; }

    public IRelayCommand SaveCommand => Request.SavePlanDetailsCommand;
    public IRelayCommand CloseCommand => Request.ClosePlanDetailsCommand;

    [ObservableProperty]
    private bool canSave;
}
