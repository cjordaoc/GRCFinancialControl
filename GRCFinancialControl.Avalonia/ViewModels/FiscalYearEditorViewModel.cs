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
            get => ConvertToOffset(PeriodStart);
            set
            {
                if (value.HasValue)
                {
                    PeriodStart = value.Value.Date;
                }
                else
                {
                    PeriodStart = default;
                }
            }
        }

        public DateTimeOffset? PeriodEndOffset
        {
            get => ConvertToOffset(PeriodEnd);
            set
            {
                if (value.HasValue)
                {
                    PeriodEnd = value.Value.Date;
                }
                else
                {
                    PeriodEnd = default;
                }
            }
        }

        public FiscalYear FiscalYear { get; }

        public FiscalYearEditorViewModel(FiscalYear fiscalYear, IFiscalYearService fiscalYearService, IMessenger messenger)
        {
            FiscalYear = fiscalYear;
            _fiscalYearService = fiscalYearService;
            _messenger = messenger;

            Name = fiscalYear.Name;
            PeriodStart = fiscalYear.PeriodStart;
            PeriodEnd = fiscalYear.PeriodEnd;
        }

        [RelayCommand]
        private async Task Save()
        {
            FiscalYear.Name = Name;
            FiscalYear.PeriodStart = PeriodStart;
            FiscalYear.PeriodEnd = PeriodEnd;

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

        private static DateTimeOffset? ConvertToOffset(DateTime value)
        {
            if (value == default)
            {
                return null;
            }

            var unspecified = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecified, TimeSpan.Zero);
        }
    }
}
