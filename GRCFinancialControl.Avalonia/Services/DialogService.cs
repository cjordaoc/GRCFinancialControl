using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using App.Presentation.Services;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRC.Shared.UI.Dialogs;

namespace GRCFinancialControl.Avalonia.Services;

/// <summary>
/// GRC-specific dialog service with centered modal layout and confirmation helper.
/// </summary>
public sealed class DialogService : BaseDialogService
{
    private readonly IMessenger _messenger;
    private readonly ViewLocator _viewLocator = new();

    public DialogService(IMessenger messenger, IModalDialogService modalDialogService)
        : base(messenger, modalDialogService)
    {
        _messenger = messenger;
    }

    /// <summary>
    /// Displays a modal dialog for the given view model.
    /// </summary>
    public Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null)
        => base.ShowDialogAsync(viewModel, title);

    /// <summary>
    /// Displays a confirmation dialog with Yes/No buttons.
    /// </summary>
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var confirmationViewModel = new ConfirmationDialogViewModel(title, message, _messenger);
        return ShowDialogAsync(confirmationViewModel, title);
    }

    protected override UserControl? BuildView(object viewModel)
        => _viewLocator.Build(viewModel) as UserControl;
}
