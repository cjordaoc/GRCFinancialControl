using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Services;
using GRC.Shared.UI.Dialogs;
using GRC.Shared.UI.ViewModels.Dialogs;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia.Services;

/// <summary>
/// Invoice Planner dialog service with owner-aligned layout and nested dialog support.
/// </summary>
public sealed class DialogService : BaseDialogService
{
    private readonly ViewLocator _viewLocator = new();

    public DialogService(IMessenger messenger, IModalDialogService modalDialogService)
        : base(messenger, modalDialogService)
    {
    }

    /// <summary>
    /// Displays a modal dialog for the given view model.
    /// </summary>
    public Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null)
        => base.ShowDialogAsync(viewModel, title);

    protected override UserControl? BuildView(object viewModel)
        => _viewLocator.Build(viewModel) as UserControl;

    protected override ModalDialogOptions GetModalDialogOptions()
        => new()
        {
            Layout = ModalDialogLayout.OwnerAligned,
            SizeRatioResourceKey = "DialogContentRatioStandard",
            ShowWindowControls = false,
            DimBackground = true,
            FreezeOwner = true
        };

    protected override DialogFocusState OnDialogOpening(Window dialog, Window owner, Window? previousDialog, ModalDialogOptions options)
    {
        var focusScope = previousDialog ?? owner;
        var previousFocus = focusScope.FocusManager?.GetFocusedElement();

        if (options.FreezeOwner)
        {
            if (previousDialog is null)
            {
                owner.IsEnabled = false;
            }
            else
            {
                previousDialog.IsEnabled = false;
            }
        }

        return new DialogFocusState(previousDialog, previousFocus);
    }

    protected override void OnDialogClosing(Window dialog, Window owner, DialogFocusState focusState, ModalDialogOptions options)
    {
        var restoredDialog = CurrentDialog;

        Dispatcher.UIThread.Post(() =>
        {
            if (options.FreezeOwner)
            {
                if (restoredDialog is not null)
                {
                    // Nested dialog: re-enable previous dialog and restore its focus
                    restoredDialog.IsEnabled = true;
                    if (focusState.PreviousFocus is Control control)
                    {
                        control.Focus();
                    }
                    else
                    {
                        restoredDialog.Focus();
                    }
                }
                else
                {
                    // Top-level dialog: re-enable owner and restore focus
                    owner.IsEnabled = true;
                    focusState.PreviousFocus?.Focus();
                }
            }
            else
            {
                focusState.PreviousFocus?.Focus();
            }
        }, DispatcherPriority.Background);
    }
}
