using System;
using System.Threading.Tasks;
using App.Presentation.Controls;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.Services;

public class DialogService : IDialogService
{
    private readonly IMessenger _messenger;
    private readonly ViewLocator _viewLocator = new();
    private IModalOverlayHost? _overlayHost;
    private bool _isClosingFromHost;

    public DialogService(IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register<DialogService, CloseDialogMessage>(this, static (recipient, message) =>
        {
            recipient.OnCloseDialogRequested(message.Value);
        });
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

        return await _overlayHost.ShowModalAsync(view, title, canClose) ?? false;
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

        _isClosingFromHost = true;
        try
        {
            _messenger.Send(new CloseDialogMessage(e.Result));
            _overlayHost.Close(e.Result);
        }
        finally
        {
            _isClosingFromHost = false;
        }
    }

    private void OnCloseDialogRequested(bool result)
    {
        if (_isClosingFromHost)
        {
            return;
        }

        _overlayHost?.Close(result);
    }
}
