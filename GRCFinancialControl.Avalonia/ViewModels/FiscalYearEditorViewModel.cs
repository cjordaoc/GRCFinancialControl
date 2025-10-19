using System;
using System.Threading.Tasks;
using App.Presentation.Localization;
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

        [ObservableProperty]
        private bool _isReadOnlyMode;

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

        public bool AllowEditing => !IsReadOnlyMode;

        public FiscalYearEditorViewModel(
            FiscalYear fiscalYear,
            IFiscalYearService fiscalYearService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
        {
            FiscalYear = fiscalYear;
            _fiscalYearService = fiscalYearService;
            _messenger = messenger;

            Name = fiscalYear.Name;
            StartDate = fiscalYear.StartDate;
            EndDate = fiscalYear.EndDate;
            AreaSalesTarget = fiscalYear.AreaSalesTarget;
            AreaRevenueTarget = fiscalYear.AreaRevenueTarget;
            IsReadOnlyMode = isReadOnlyMode;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            if (IsReadOnlyMode)
            {
                return;
            }

            StatusMessage = null;

            if (string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = LocalizationRegistry.Get("FiscalYears.Validation.NameRequired");
                return;
            }

            if (EndDate < StartDate)
            {
                StatusMessage = LocalizationRegistry.Get("FiscalYears.Validation.EndDateAfterStart");
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

        private bool CanSave() => !IsReadOnlyMode;

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
        }
    }
}
