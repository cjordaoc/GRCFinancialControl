using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using System.Threading.Tasks;

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
            Messenger = messenger;
            Messenger.RegisterAll(this);
        }

        public virtual Task LoadDataAsync() => Task.CompletedTask;

        public virtual void Receive(RefreshDataMessage message)
        {
            _ = LoadDataAsync();
        }
    }
}
