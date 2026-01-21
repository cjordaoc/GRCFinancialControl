using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRC.Shared.UI.Messages;
using GRC.Shared.UI.ViewModels.Dialogs;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ConfirmationDialogViewModel : ConfirmationDialogViewModelBase
    {
        private readonly IMessenger _messenger;

        public IRelayCommand SaveCommand => ConfirmCommand;
        public IRelayCommand CloseCommand => CancelCommand;

        public ConfirmationDialogViewModel(string title, string message, IMessenger messenger)
        {
            _messenger = messenger;
            Title = title;
            Message = message;
            ConfirmButtonText = LocalizationRegistry.Get("Global_Button_Save");
            CancelButtonText = LocalizationRegistry.Get("Global_Button_Cancel");

            OnConfirmed = () => _messenger.Send(new CloseDialogMessage(true));
            OnCanceled = () => _messenger.Send(new CloseDialogMessage(false));
        }

        public ConfirmationDialogViewModel()
            : this(string.Empty, string.Empty, WeakReferenceMessenger.Default)
        {
        }
    }
}
