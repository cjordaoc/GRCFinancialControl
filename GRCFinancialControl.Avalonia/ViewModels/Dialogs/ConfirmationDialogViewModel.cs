using System.Windows.Input;
using App.Presentation.Controls;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ConfirmationDialogViewModel : ViewModelBase, IModalOverlayActionProvider
    {
        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _message;

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public bool IsPrimaryActionVisible => true;

        public string? PrimaryActionText => LocalizationRegistry.Get("Common.Button.Confirm");

        public ICommand? PrimaryActionCommand => ConfirmCommand;

        public ConfirmationDialogViewModel(string title, string message, IMessenger messenger)
            : base(messenger)
        {
            _title = title;
            _message = message;
            ConfirmCommand = new RelayCommand(OnConfirm);
            CancelCommand = new RelayCommand(OnCancel);
        }

        private void OnConfirm()
        {
            Messenger.Send(new CloseDialogMessage(true));
        }

        private void OnCancel()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }
    }
}
