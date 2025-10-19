using System.Threading.Tasks;
using System.Windows.Input;
using App.Presentation.Controls;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public abstract partial class DialogEditorViewModel<TEntity> : ViewModelBase, IModalOverlayActionProvider
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

        public virtual bool IsPrimaryActionVisible => AllowEditing;

        public virtual string? PrimaryActionText => LocalizationRegistry.Get("Common.Button.Save");

        public virtual ICommand? PrimaryActionCommand => SaveCommand;

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
            OnPropertyChanged(nameof(IsPrimaryActionVisible));
        }

        protected abstract Task PersistChangesAsync();
    }
}
