using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Models;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class PapdTransferViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<Papd> _availablePapds = new();

        [ObservableProperty]
        private Papd? _selectedPapd;

        public string Title => "Transfer Assignments";

        public PapdTransferViewModel(
            IList<Papd> availablePapds,
            IMessenger messenger)
            : base(messenger)
        {
            AvailablePapds = new ObservableCollection<Papd>(
                availablePapds
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }

        public override Task LoadDataAsync()
        {
            return Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanTransfer))]
        private void Transfer()
        {
            Messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Cancel()
        {
            SelectedPapd = null;
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanTransfer() => SelectedPapd is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            TransferCommand.NotifyCanExecuteChanged();
        }
    }
}
