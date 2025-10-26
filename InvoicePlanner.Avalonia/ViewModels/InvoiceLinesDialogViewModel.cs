using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;

namespace InvoicePlanner.Avalonia.ViewModels
{
    public partial class InvoiceLinesDialogViewModel : ViewModelBase
    {
        public InvoiceLinesDialogViewModel(ObservableCollection<InvoicePlanLineViewModel> items, IMessenger messenger) : base(messenger)
        {
            Items = items;
            SaveCommand = new RelayCommand(SaveChanges);
            CloseCommand = new RelayCommand(CloseDialog);
        }

        public ObservableCollection<InvoicePlanLineViewModel> Items { get; }

        public IRelayCommand SaveCommand { get; }
        public IRelayCommand CloseCommand { get; }

        [ObservableProperty]
        private bool canSave = true;

        private void SaveChanges()
        {
            // TODO: Implement save logic, likely by sending a message back to the parent view model
            Messenger.Send(new CloseDialogMessage(true)); // Assuming true means "changes were made"
        }

        private void CloseDialog()
        {
            Messenger.Send(new CloseDialogMessage(false)); // False means "no changes" or "cancelled"
        }
    }
}
