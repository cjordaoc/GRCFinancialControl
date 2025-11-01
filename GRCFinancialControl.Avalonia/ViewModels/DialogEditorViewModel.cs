using System;
using System.Threading.Tasks;
using System.Windows.Input;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public abstract partial class DialogEditorViewModel<TEntity> : ViewModelBase
        where TEntity : class
    {
        protected DialogEditorViewModel(IMessenger messenger, bool isReadOnlyMode = false)
            : base(messenger)
        {
            IsReadOnlyMode = isReadOnlyMode;
            ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
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

            try
            {
                await PersistChangesAsync();
                OnSaveSucceeded();
                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                OnSaveFailed(ex);
            }
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => !IsReadOnlyMode && !HasErrors;

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
        }

        protected abstract Task PersistChangesAsync();

        protected virtual void OnSaveSucceeded()
        {
        }

        protected virtual void OnSaveFailed(Exception exception)
        {
        }
    }
}
