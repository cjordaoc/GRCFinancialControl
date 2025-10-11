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

        public FiscalYear FiscalYear { get; }

        public FiscalYearEditorViewModel(FiscalYear fiscalYear, IFiscalYearService fiscalYearService, IMessenger messenger)
        {
            FiscalYear = fiscalYear;
            _fiscalYearService = fiscalYearService;
            _messenger = messenger;

            Name = fiscalYear.Name;
            StartDate = fiscalYear.StartDate;
            EndDate = fiscalYear.EndDate;
        }

        [RelayCommand]
        private async Task Save()
        {
            FiscalYear.Name = Name;
            FiscalYear.StartDate = StartDate;
            FiscalYear.EndDate = EndDate;

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
        private void Cancel()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }
}