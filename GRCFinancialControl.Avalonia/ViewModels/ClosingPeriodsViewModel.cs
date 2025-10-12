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

        public ClosingPeriodsViewModel(IClosingPeriodService closingPeriodService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _closingPeriodService = closingPeriodService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            StatusMessage = null;
            var periods = await _closingPeriodService.GetAllAsync();
            ClosingPeriods = new ObservableCollection<ClosingPeriod>(periods);
        }

        [RelayCommand]
        private async Task Add()
        {
            var editor = new ClosingPeriodEditorViewModel(new ClosingPeriod
            {
                Name = string.Empty,
                PeriodStart = DateTime.Today,
                PeriodEnd = DateTime.Today
            }, _closingPeriodService, Messenger);

            await _dialogService.ShowDialogAsync(editor);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Edit(ClosingPeriod closingPeriod)
        {
            if (closingPeriod == null)
            {
                return;
            }

            var editor = new ClosingPeriodEditorViewModel(closingPeriod, _closingPeriodService, Messenger);
            await _dialogService.ShowDialogAsync(editor);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Delete(ClosingPeriod closingPeriod)
        {
            if (closingPeriod == null)
            {
                return;
            }

            try
            {
                await _closingPeriodService.DeleteAsync(closingPeriod.Id);
                Messenger.Send(new RefreshDataMessage());
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
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
