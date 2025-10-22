using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public abstract partial class ViewModelBase : ObservableObject, IRecipient<RefreshDataMessage>
    {
        protected IMessenger Messenger { get; }

        protected ViewModelBase() : this(WeakReferenceMessenger.Default)
        {
        }

        protected ViewModelBase(IMessenger messenger)
        {
            ArgumentNullException.ThrowIfNull(messenger);

            Messenger = messenger;
            Messenger.RegisterAll(this);
        }

        public virtual Task LoadDataAsync() => Task.CompletedTask;

        public virtual void Receive(RefreshDataMessage message)
        {
            _ = LoadDataAsync();
        }

        protected static void NotifyCommandCanExecute(IRelayCommand? command)
        {
            if (command is null)
            {
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                command.NotifyCanExecuteChanged();
            }
            else
            {
                Dispatcher.UIThread.Post(command.NotifyCanExecuteChanged);
            }
        }
    }
}
