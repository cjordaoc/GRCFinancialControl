using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public abstract partial class DialogEditorViewModel<TEntity> : ViewModelBase
        where TEntity : class
    {
        protected DialogEditorViewModel(IMessenger messenger, bool isReadOnlyMode = false)
            : base(messenger)
        {
            IsReadOnlyMode = isReadOnlyMode;
        }

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public bool AllowEditing => !IsReadOnlyMode;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (IsReadOnlyMode)
            {
                return;
            }

            await PersistChangesAsync();
            Messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => !IsReadOnlyMode;

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
        }

        protected abstract Task PersistChangesAsync();
    }
}
