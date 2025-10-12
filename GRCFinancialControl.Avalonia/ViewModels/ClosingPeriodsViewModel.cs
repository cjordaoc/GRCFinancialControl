using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ClosingPeriodsViewModel : ViewModelBase, IRecipient<ClosingPeriodsChangedMessage>
    {
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private string? _statusMessage;

        public ClosingPeriodsViewModel(IClosingPeriodService closingPeriodService, IMessenger messenger)
        {
            _closingPeriodService = closingPeriodService;
            _messenger = messenger;

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit, () => SelectedClosingPeriod != null);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedClosingPeriod != null);

            _messenger.Register<ClosingPeriodsChangedMessage>(this);
        }

        public IRelayCommand AddCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }

        public override async Task LoadDataAsync()
        {
            StatusMessage = null;
            var periods = await _closingPeriodService.GetAllAsync();
            ClosingPeriods = new ObservableCollection<ClosingPeriod>(periods);
        }

        private void Add()
        {
            var editor = new ClosingPeriodEditorViewModel(new ClosingPeriod
            {
                Name = string.Empty,
                PeriodStart = DateTime.Today,
                PeriodEnd = DateTime.Today
            }, _closingPeriodService, _messenger);

            _messenger.Send(new OpenDialogMessage(editor));
        }

        private void Edit()
        {
            if (SelectedClosingPeriod == null)
            {
                return;
            }

            var editor = new ClosingPeriodEditorViewModel(SelectedClosingPeriod, _closingPeriodService, _messenger);
            _messenger.Send(new OpenDialogMessage(editor));
        }

        private async Task DeleteAsync()
        {
            if (SelectedClosingPeriod == null)
            {
                return;
            }

            try
            {
                await _closingPeriodService.DeleteAsync(SelectedClosingPeriod.Id);
                await LoadDataAsync();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
        }

        public void Receive(ClosingPeriodsChangedMessage message)
        {
            _ = LoadDataAsync();
        }

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            if (EditCommand is RelayCommand editCommand)
            {
                editCommand.NotifyCanExecuteChanged();
            }

            if (DeleteCommand is AsyncRelayCommand deleteCommand)
            {
                deleteCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
