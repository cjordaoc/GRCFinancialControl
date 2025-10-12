using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Services
{
    public class DialogService : IDialogService, IRecipient<CloseDialogMessage>
    {
        private readonly IMessenger _messenger;
        private TaskCompletionSource<bool>? _currentRequest;

        public DialogService(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register<CloseDialogMessage>(this);
        }

        public Task<bool> ShowDialogAsync(ViewModelBase viewModel)
        {
            if (_currentRequest != null)
            {
                throw new InvalidOperationException("A dialog is already open.");
            }

            var tcs = new TaskCompletionSource<bool>();
            _currentRequest = tcs;
            _messenger.Send(new OpenDialogMessage(viewModel));
            return tcs.Task;
        }

        public void Receive(CloseDialogMessage message)
        {
            _currentRequest?.TrySetResult(message.Value);
            _currentRequest = null;
        }
    }
}
