using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Utilities;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class FiscalYearEditorViewModel : ViewModelBase
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private decimal _areaSalesTarget;

        [ObservableProperty]
        private decimal _areaRevenueTarget;

        [ObservableProperty]
        private string? _statusMessage;

        partial void OnStartDateChanged(DateTime value)
        {
            OnPropertyChanged(nameof(StartDateOffset));
        }

        partial void OnEndDateChanged(DateTime value)
        {
            OnPropertyChanged(nameof(EndDateOffset));
        }

        public DateTimeOffset? StartDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(StartDate);
            set
            {
                StartDate = DateTimeOffsetHelper.ToDate(value) ?? default;
            }
        }

        public DateTimeOffset? EndDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(EndDate);
            set
            {
                EndDate = DateTimeOffsetHelper.ToDate(value) ?? default;
            }
        }

        public FiscalYear FiscalYear { get; }

        public FiscalYearEditorViewModel(FiscalYear fiscalYear, IFiscalYearService fiscalYearService, IMessenger messenger)
        {
            FiscalYear = fiscalYear;
            _fiscalYearService = fiscalYearService;
            _messenger = messenger;

            Name = fiscalYear.Name;
            StartDate = fiscalYear.StartDate;
            EndDate = fiscalYear.EndDate;
            AreaSalesTarget = fiscalYear.AreaSalesTarget;
            AreaRevenueTarget = fiscalYear.AreaRevenueTarget;
        }

        [RelayCommand]
        private async Task Save()
        {
            StatusMessage = null;

            if (string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = "A fiscal year name is required.";
                return;
            }

            if (EndDate < StartDate)
            {
                StatusMessage = "The end date must be on or after the start date.";
                return;
            }

            FiscalYear.Name = Name.Trim();
            FiscalYear.StartDate = StartDate.Date;
            FiscalYear.EndDate = EndDate.Date;
            FiscalYear.AreaSalesTarget = AreaSalesTarget;
            FiscalYear.AreaRevenueTarget = AreaRevenueTarget;

            if (FiscalYear.Id == 0)
            {
                await _fiscalYearService.AddAsync(FiscalYear);
            }
            else
            {
                await _fiscalYearService.UpdateAsync(FiscalYear);
            }

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }
}
