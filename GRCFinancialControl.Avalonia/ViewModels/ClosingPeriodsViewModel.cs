using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ClosingPeriodsViewModel : ViewModelBase, IRecipient<ClosingPeriodsChangedMessage>
    {
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IDialogService _dialogService;
        private readonly DataBackendOptions _dataBackendOptions;

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private string? _statusMessage;

        public ClosingPeriodsViewModel(
            IClosingPeriodService closingPeriodService,
            IFiscalYearService fiscalYearService,
            IDialogService dialogService,
            IMessenger messenger,
            DataBackendOptions dataBackendOptions)
            : base(messenger)
        {
            _closingPeriodService = closingPeriodService;
            _fiscalYearService = fiscalYearService;
            _dialogService = dialogService;
            _dataBackendOptions = dataBackendOptions;
        }

        public bool HasUnlockedFiscalYears => FiscalYears.Any(fy => !fy.IsLocked);

        public bool AreAllFiscalYearsLocked => !HasUnlockedFiscalYears;

        public override async Task LoadDataAsync()
        {
            StatusMessage = null;
            if (!IsMutationSupported)
            {
                StatusMessage = "Closing period maintenance is read-only when the Dataverse backend is active.";
            }
            var periods = await _closingPeriodService.GetAllAsync();
            ClosingPeriods = new ObservableCollection<ClosingPeriod>(periods);

            var fiscalYears = await _fiscalYearService.GetAllAsync();
            FiscalYears = new ObservableCollection<FiscalYear>(fiscalYears.OrderBy(fy => fy.StartDate));
        }

        [RelayCommand(CanExecute = nameof(CanAdd))]
        private async Task Add()
        {
            StatusMessage = null;

            var newClosingPeriod = new ClosingPeriod
            {
                Name = string.Empty,
                PeriodStart = DateTime.Today,
                PeriodEnd = DateTime.Today
            };

            var selectableFiscalYears = GetSelectableFiscalYears(newClosingPeriod);
            if (selectableFiscalYears.Count == 0)
            {
                StatusMessage = "All fiscal years are locked. Unlock a fiscal year before adding closing periods.";
                return;
            }

            var editor = new ClosingPeriodEditorViewModel(newClosingPeriod, selectableFiscalYears, _closingPeriodService, Messenger);

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

            StatusMessage = null;

            if (closingPeriod.FiscalYear?.IsLocked ?? false)
            {
                StatusMessage = $"Fiscal year '{FormatFiscalYearName(closingPeriod)}' is locked. Unlock it before editing closing periods.";
                return;
            }

            var editor = new ClosingPeriodEditorViewModel(closingPeriod, GetSelectableFiscalYears(closingPeriod), _closingPeriodService, Messenger);
            await _dialogService.ShowDialogAsync(editor);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanView))]
        private async Task View(ClosingPeriod closingPeriod)
        {
            if (closingPeriod == null)
            {
                return;
            }

            var editor = new ClosingPeriodEditorViewModel(closingPeriod,
                GetSelectableFiscalYears(closingPeriod),
                _closingPeriodService,
                Messenger,
                isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editor);
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
                StatusMessage = null;

                if (closingPeriod.FiscalYear?.IsLocked ?? false)
                {
                    StatusMessage = $"Fiscal year '{FormatFiscalYearName(closingPeriod)}' is locked. Unlock it before deleting closing periods.";
                    return;
                }

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

            StatusMessage = null;

            if (closingPeriod.FiscalYear?.IsLocked ?? false)
            {
                StatusMessage = $"Fiscal year '{FormatFiscalYearName(closingPeriod)}' is locked. Unlock it before deleting data.";
                return;
            }

            var result = await _dialogService.ShowConfirmationAsync("Delete Data", $"Are you sure you want to delete all data for {closingPeriod.Name}? This action cannot be undone.");
            if (result)
            {
                try
                {
                    await _closingPeriodService.DeleteDataAsync(closingPeriod.Id);
                    Messenger.Send(new RefreshDataMessage());
                }
                catch (InvalidOperationException ex)
                {
                    StatusMessage = ex.Message;
                }
            }
        }

        private bool CanAdd() => IsMutationSupported && HasUnlockedFiscalYears;

        private bool CanEdit(ClosingPeriod closingPeriod) => IsMutationSupported && closingPeriod is not null && !closingPeriod.IsLocked;

        private bool CanDelete(ClosingPeriod closingPeriod) => IsMutationSupported && closingPeriod is not null && !closingPeriod.IsLocked;

        private bool CanDeleteData(ClosingPeriod closingPeriod) => IsMutationSupported && closingPeriod is not null && !closingPeriod.IsLocked;

        private static bool CanView(ClosingPeriod closingPeriod) => closingPeriod is not null;

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

        partial void OnFiscalYearsChanging(ObservableCollection<FiscalYear> value)
        {
            if (value != null)
            {
                value.CollectionChanged -= OnFiscalYearsCollectionChanged;
            }
        }

        partial void OnFiscalYearsChanged(ObservableCollection<FiscalYear> value)
        {
            AddCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedFiscalYears));
            OnPropertyChanged(nameof(AreAllFiscalYearsLocked));
            if (value != null)
            {
                value.CollectionChanged += OnFiscalYearsCollectionChanged;
            }
        }

        partial void OnClosingPeriodsChanging(ObservableCollection<ClosingPeriod> value)
        {
            if (value != null)
            {
                value.CollectionChanged -= OnClosingPeriodsCollectionChanged;
            }
        }

        partial void OnClosingPeriodsChanged(ObservableCollection<ClosingPeriod> value)
        {
            if (value != null)
            {
                value.CollectionChanged += OnClosingPeriodsCollectionChanged;
            }
        }

        private void OnFiscalYearsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AddCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedFiscalYears));
            OnPropertyChanged(nameof(AreAllFiscalYearsLocked));
        }

        private void OnClosingPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
        }

        public void Receive(ClosingPeriodsChangedMessage message)
        {
            _ = LoadDataAsync();
        }

        private List<FiscalYear> GetSelectableFiscalYears(ClosingPeriod closingPeriod)
        {
            return FiscalYears
                .Where(fy => !fy.IsLocked || fy.Id == closingPeriod.FiscalYearId)
                .OrderBy(fy => fy.StartDate)
                .ToList();
        }

        private static string FormatFiscalYearName(ClosingPeriod closingPeriod)
        {
            if (closingPeriod.FiscalYear != null && !string.IsNullOrWhiteSpace(closingPeriod.FiscalYear.Name))
            {
                return closingPeriod.FiscalYear.Name;
            }

            return $"Id={closingPeriod.FiscalYearId}";
        }

        private bool IsMutationSupported => _dataBackendOptions.Backend == DataBackend.MySql;
    }
}
