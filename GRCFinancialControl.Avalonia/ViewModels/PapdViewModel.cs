using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdViewModel : ViewModelBase, IRecipient<RefreshDataMessage>
    {
        private readonly IPapdService _papdService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Papd? _selectedPapd;

        public PapdViewModel(IPapdService papdService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _papdService = papdService;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        public override async Task LoadDataAsync()
        {
            Papds = new ObservableCollection<Papd>(await _papdService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new PapdEditorViewModel(new Papd(), _papdService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Edit(Papd papd)
        {
            if (papd == null) return;
            var editorViewModel = new PapdEditorViewModel(papd, _papdService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Delete(Papd papd)
        {
            if (papd == null) return;
            await _papdService.DeleteAsync(papd.Id);
            Messenger.Send(new RefreshDataMessage());
        }

        partial void OnSelectedPapdChanged(Papd? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        public void Receive(RefreshDataMessage message)
        {
            _ = LoadDataAsync();
        }
    }
}