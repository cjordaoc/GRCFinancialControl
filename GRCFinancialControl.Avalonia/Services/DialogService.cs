using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Controls;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;

namespace GRCFinancialControl.Avalonia.Services
{
    public class DialogService : IDialogService, IRecipient<CloseDialogMessage>
    {
        private readonly IMessenger _messenger;
        private readonly Stack<TaskCompletionSource<bool>> _dialogStack = new();
        private IModalOverlayHost? _overlayHost;

        public DialogService(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register<CloseDialogMessage>(this);
        }

        public Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null, bool canClose = true)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dialogStack.Push(tcs);
            _messenger.Send(new OpenDialogMessage(viewModel, title, canClose));
            return tcs.Task;
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
        }

        public void Receive(CloseDialogMessage message)
        {
            if (_dialogStack.TryPop(out var tcs))
            {
                tcs.TrySetResult(message.Value);
            }

            if (_overlayHost?.IsOverlayOpen == true)
            {
                _overlayHost.Close(message.Value);
            }
        }
    }
}
