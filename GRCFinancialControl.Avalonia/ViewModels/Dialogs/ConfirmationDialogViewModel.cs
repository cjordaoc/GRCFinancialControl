using System.Windows.Input;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ConfirmationDialogViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _message;

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand SaveCommand => ConfirmCommand;
        public IRelayCommand CloseCommand => CancelCommand;

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
