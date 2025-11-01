using System;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
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
                const string validationKey = "FiscalYears.Validation.NameRequired";
                StatusMessage = LocalizationRegistry.Get(validationKey);
                ToastService.ShowWarning(validationKey);
                return;
            }

            if (EndDate < StartDate)
            {
                const string validationKey = "FiscalYears.Validation.EndDateAfterStart";
                StatusMessage = LocalizationRegistry.Get(validationKey);
                ToastService.ShowWarning(validationKey);
                return;
            }

            FiscalYear.Name = Name.Trim();
            FiscalYear.StartDate = StartDate.Date;
            FiscalYear.EndDate = EndDate.Date;
            FiscalYear.AreaSalesTarget = AreaSalesTarget;
            FiscalYear.AreaRevenueTarget = AreaRevenueTarget;

            try
            {
                if (FiscalYear.Id == 0)
                {
                    await _fiscalYearService.AddAsync(FiscalYear);
                }
                else
                {
                    await _fiscalYearService.UpdateAsync(FiscalYear);
                }

                ToastService.ShowSuccess("FiscalYears.Toast.SaveSuccess", FiscalYear.Name);
                _messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowError("FiscalYears.Toast.OperationFailed", ex.Message);
            }
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
