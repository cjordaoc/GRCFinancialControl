using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public abstract partial class DialogEditorViewModel<TEntity> : ViewModelBase
        where TEntity : class
    {
        protected DialogEditorViewModel(IMessenger messenger)
            : base(messenger)
        {
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            await PersistChangesAsync();
            Messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        protected abstract Task PersistChangesAsync();
    }
}
