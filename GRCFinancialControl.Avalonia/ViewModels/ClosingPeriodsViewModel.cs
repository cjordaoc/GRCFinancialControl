using System;
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
    public partial class ClosingPeriodsViewModel : ViewModelBase, IRecipient<ClosingPeriodsChangedMessage>
    {
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IDialogService _dialogService;

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

        [RelayCommand(CanExecute = nameof(CanEdit))]
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

        [RelayCommand(CanExecute = nameof(CanDelete))]
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

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(ClosingPeriod closingPeriod)
        {
            if (closingPeriod is null) return;

            var result = await _dialogService.ShowConfirmationAsync("Delete Data", $"Are you sure you want to delete all data for {closingPeriod.Name}? This action cannot be undone.");
            if (result)
            {
                await _closingPeriodService.DeleteDataAsync(closingPeriod.Id);
                Messenger.Send(new RefreshDataMessage());
            }
        }

        private static bool CanEdit(ClosingPeriod closingPeriod) => closingPeriod is not null;

        private static bool CanDelete(ClosingPeriod closingPeriod) => closingPeriod is not null;

        private static bool CanDeleteData(ClosingPeriod closingPeriod) => closingPeriod is not null;

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
        }

        public void Receive(ClosingPeriodsChangedMessage message)
        {
            _ = LoadDataAsync();
        }
    }
}
