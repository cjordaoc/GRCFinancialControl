using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ClosingPeriodEditorViewModel : ViewModelBase
    {
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private DateTime _periodStart = DateTime.Today;

        [ObservableProperty]
        private DateTime _periodEnd = DateTime.Today;

        [ObservableProperty]
        private string? _statusMessage;

        public ClosingPeriod ClosingPeriod { get; }

        public ClosingPeriodEditorViewModel(ClosingPeriod closingPeriod, IClosingPeriodService closingPeriodService, IMessenger messenger)
        {
            ClosingPeriod = closingPeriod;
            _closingPeriodService = closingPeriodService;
            _messenger = messenger;

            _name = closingPeriod.Name;
            _periodStart = closingPeriod.PeriodStart == default ? DateTime.Today : closingPeriod.PeriodStart;
            _periodEnd = closingPeriod.PeriodEnd == default ? DateTime.Today : closingPeriod.PeriodEnd;
        }

        [RelayCommand]
        private async Task Save()
        {
            StatusMessage = null;

            if (string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = "A descriptive name is required.";
                return;
            }

            if (PeriodEnd < PeriodStart)
            {
                StatusMessage = "The end date must be on or after the start date.";
                return;
            }

            ClosingPeriod.Name = Name.Trim();
            ClosingPeriod.PeriodStart = PeriodStart.Date;
            ClosingPeriod.PeriodEnd = PeriodEnd.Date;

            if (ClosingPeriod.Id == 0)
            {
                await _closingPeriodService.AddAsync(ClosingPeriod);
            }
            else
            {
                await _closingPeriodService.UpdateAsync(ClosingPeriod);
            }

            _messenger.Send(new ClosingPeriodsChangedMessage());
            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Cancel()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }
}
