using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public DialogService(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register<CloseDialogMessage>(this);
        }

        public Task<bool> ShowDialogAsync(ViewModelBase viewModel)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dialogStack.Push(tcs);
            _messenger.Send(new OpenDialogMessage(viewModel));
            return tcs.Task;
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var confirmationViewModel = new ConfirmationDialogViewModel(title, message, _messenger);
            return ShowDialogAsync(confirmationViewModel);
        }

        public void Receive(CloseDialogMessage message)
        {
            if (_dialogStack.TryPop(out var tcs))
            {
                tcs.TrySetResult(message.Value);
            }
        }
    }
}
