using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    public partial class ClosingPeriodEditorViewModel : ViewModelBase
    {
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private FiscalYear? _selectedFiscalYear;

        [ObservableProperty]
        private DateTime _periodStart = DateTime.Today;

        [ObservableProperty]
        private DateTime _periodEnd = DateTime.Today;

        partial void OnPeriodStartChanged(DateTime value)
        {
            OnPropertyChanged(nameof(PeriodStartOffset));
        }

        partial void OnPeriodEndChanged(DateTime value)
        {
            OnPropertyChanged(nameof(PeriodEndOffset));
        }

        public DateTimeOffset? PeriodStartOffset
        {
            get => DateTimeOffsetHelper.FromDate(PeriodStart);
            set
            {
                PeriodStart = DateTimeOffsetHelper.ToDate(value) ?? default;
            }
        }

        public DateTimeOffset? PeriodEndOffset
        {
            get => DateTimeOffsetHelper.FromDate(PeriodEnd);
            set
            {
                PeriodEnd = DateTimeOffsetHelper.ToDate(value) ?? default;
            }
        }

        [ObservableProperty]
        private string? _statusMessage;

        public ClosingPeriod ClosingPeriod { get; }

        public bool HasFiscalYears => FiscalYears.Count > 0;

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public bool AllowEditing => !IsReadOnlyMode;

        public ClosingPeriodEditorViewModel(
            ClosingPeriod closingPeriod,
            IEnumerable<FiscalYear> fiscalYears,
            IClosingPeriodService closingPeriodService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
        {
            ClosingPeriod = closingPeriod;
            _closingPeriodService = closingPeriodService;
            _messenger = messenger;

            ArgumentNullException.ThrowIfNull(fiscalYears);

            _name = closingPeriod.Name;
            _periodStart = closingPeriod.PeriodStart == default ? DateTime.Today : closingPeriod.PeriodStart;
            _periodEnd = closingPeriod.PeriodEnd == default ? DateTime.Today : closingPeriod.PeriodEnd;

            var fiscalYearList = fiscalYears.ToList();
            FiscalYears = new ObservableCollection<FiscalYear>(fiscalYearList.OrderBy(fy => fy.StartDate));

            SelectedFiscalYear = FiscalYears.FirstOrDefault(fy => fy.Id == closingPeriod.FiscalYearId)
                ?? FiscalYears.FirstOrDefault();

            if (SelectedFiscalYear is null)
            {
                StatusMessage = LocalizationRegistry.Get("ClosingPeriods.Validation.NoFiscalYears");
            }

            IsReadOnlyMode = isReadOnlyMode;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            if (!CanSave())
            {
                return;
            }

            StatusMessage = null;

            if (SelectedFiscalYear is null)
            {
                StatusMessage = LocalizationRegistry.Get("ClosingPeriods.Validation.FiscalYearRequired");
                return;
            }

            if (SelectedFiscalYear.IsLocked)
            {
                var fiscalYearName = string.IsNullOrWhiteSpace(SelectedFiscalYear.Name)
                    ? $"Id={SelectedFiscalYear.Id}"
                    : SelectedFiscalYear.Name;

                StatusMessage = LocalizationRegistry.Format(
                    "ClosingPeriods.Validation.FiscalYearLocked",
                    fiscalYearName);
                return;
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = LocalizationRegistry.Get("ClosingPeriods.Validation.NameRequired");
                return;
            }

            if (PeriodEnd < PeriodStart)
            {
                StatusMessage = LocalizationRegistry.Get("ClosingPeriods.Validation.EndDateAfterStart");
                return;
            }

            ClosingPeriod.Name = Name.Trim();
            ClosingPeriod.PeriodStart = PeriodStart.Date;
            ClosingPeriod.PeriodEnd = PeriodEnd.Date;
            ClosingPeriod.FiscalYearId = SelectedFiscalYear.Id;
            ClosingPeriod.FiscalYear = SelectedFiscalYear;

            try
            {
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
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnFiscalYearsChanged(ObservableCollection<FiscalYear> value)
        {
            OnPropertyChanged(nameof(HasFiscalYears));
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedFiscalYearChanged(FiscalYear? value)
        {
            if (value is null)
            {
                return;
            }

            ClosingPeriod.FiscalYearId = value.Id;
            ClosingPeriod.FiscalYear = value;
            OnPropertyChanged(nameof(HasFiscalYears));
            SaveCommand.NotifyCanExecuteChanged();
        }

        private bool CanSave() => HasFiscalYears && !IsReadOnlyMode;

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
        }
    }
}

