using System;
using System.Threading.Tasks;
using App.Presentation.Controls;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;

namespace GRCFinancialControl.Avalonia.Services;

public class DialogService : IDialogService
{
    private readonly IMessenger _messenger;
    private readonly ViewLocator _viewLocator = new();
    private IModalOverlayHost? _overlayHost;

    public DialogService(IMessenger messenger)
    {
        _messenger = messenger;
    }

    public async Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null, bool canClose = true)
    {
        if (_overlayHost is null)
        {
            throw new InvalidOperationException("The dialog host has not been attached. Please ensure AttachHost is called before showing a dialog.");
        }

        if (_viewLocator.Build(viewModel) is not UserControl view)
        {
            throw new InvalidOperationException($"Could not locate a view for the view model '{viewModel.GetType().FullName}'.");
        }

        view.DataContext = viewModel;

        var result = await _overlayHost.ShowModalAsync(view, title, canClose);
        return result ?? false;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var confirmationViewModel = new ConfirmationDialogViewModel(title, message, _messenger);
        return ShowDialogAsync(confirmationViewModel, title);
    }

    public void AttachHost(IModalOverlayHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _overlayHost = host;
        _overlayHost.CloseRequested += OnHostCloseRequested;
    }

    private void OnHostCloseRequested(object? sender, ModalOverlayCloseRequestedEventArgs e)
    {
        if (_overlayHost is null)
        {
            return;
        }

        _messenger.Send(new Messages.CloseDialogMessage(e.Result));
        _overlayHost.Close(e.Result);
    }
}
