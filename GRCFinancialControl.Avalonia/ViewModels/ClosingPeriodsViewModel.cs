using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ClosingPeriodsViewModel : ViewModelBase, IRecipient<ClosingPeriodsChangedMessage>
    {
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IFiscalYearService _fiscalYearService;
        private readonly DialogService _dialogService;
        private readonly List<ClosingPeriod> _allClosingPeriods = new();
        private bool _isInitializingFilters;

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYearFilterOption> _fiscalYearFilters = new();

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private FiscalYearFilterOption? _selectedFiscalYearFilter;

        [ObservableProperty]
        private string? _statusMessage;

        public ClosingPeriodsViewModel(
            IClosingPeriodService closingPeriodService,
            IFiscalYearService fiscalYearService,
            DialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _closingPeriodService = closingPeriodService;
            _fiscalYearService = fiscalYearService;
            _dialogService = dialogService;
        }

        public bool HasUnlockedFiscalYears => FiscalYears.Any(fy => !fy.IsLocked);

        public bool AreAllFiscalYearsLocked => !HasUnlockedFiscalYears;

        public string LockButtonLabel => SelectedClosingPeriod?.IsLocked == true
            ? LocalizationRegistry.Get("ClosingPeriods.Button.Unlock")
            : LocalizationRegistry.Get("ClosingPeriods.Button.Lock");

        public override async Task LoadDataAsync()
        {
            var preferredFiscalYearId = SelectedFiscalYearFilter?.FiscalYearId;
            var preferredClosingPeriodId = SelectedClosingPeriod?.Id;
            await ReloadAsync(preferredFiscalYearId, preferredClosingPeriodId);
        }

        private async Task ReloadAsync(int? preferredFiscalYearId, int? preferredClosingPeriodId)
        {
            StatusMessage = null;

            var periods = await _closingPeriodService.GetAllAsync();
            _allClosingPeriods.Clear();
            _allClosingPeriods.AddRange(periods.OrderBy(cp => cp.PeriodStart));

            var fiscalYears = await _fiscalYearService.GetAllAsync();
            var orderedFiscalYears = fiscalYears.OrderBy(fy => fy.StartDate).ToList();
            FiscalYears = new ObservableCollection<FiscalYear>(orderedFiscalYears);

            UpdateFiscalYearFilters(orderedFiscalYears, preferredFiscalYearId);
            ApplyFiscalYearFilter(preferredClosingPeriodId);
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
                StatusMessage = LocalizationRegistry.Get("ClosingPeriods.Status.AllLocked");
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
                StatusMessage = LocalizationRegistry.Format(
                    "ClosingPeriods.Status.FiscalYearLockedEdit",
                    FormatFiscalYearName(closingPeriod));
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
                    StatusMessage = LocalizationRegistry.Format(
                        "ClosingPeriods.Status.FiscalYearLockedDelete",
                        FormatFiscalYearName(closingPeriod));
                    return;
                }

                await _closingPeriodService.DeleteAsync(closingPeriod.Id);
                ToastService.ShowSuccess("ClosingPeriods.Toast.Deleted", closingPeriod.Name);
                Messenger.Send(new RefreshDataMessage());
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowWarning("ClosingPeriods.Toast.DeleteFailed");
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowError("ClosingPeriods.Toast.DeleteFailed");
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(ClosingPeriod closingPeriod)
        {
            if (closingPeriod is null)
            {
                return;
            }

            StatusMessage = null;

            if (closingPeriod.FiscalYear?.IsLocked ?? false)
            {
                StatusMessage = LocalizationRegistry.Format(
                    "ClosingPeriods.Status.FiscalYearLockedData",
                    FormatFiscalYearName(closingPeriod));
                return;
            }

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Common.Dialog.DeleteData.Title"),
                LocalizationRegistry.Format("Common.Dialog.DeleteData.Message", closingPeriod.Name));
            if (!result)
            {
                return;
            }

            try
            {
                await _closingPeriodService.DeleteDataAsync(closingPeriod.Id);
                ToastService.ShowSuccess("ClosingPeriods.Toast.DataDeleted", closingPeriod.Name);
                Messenger.Send(new RefreshDataMessage());
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowWarning("ClosingPeriods.Toast.DataDeleteFailed");
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowError("ClosingPeriods.Toast.DataDeleteFailed");
            }
        }

        [RelayCommand(CanExecute = nameof(CanToggleLock))]
        private async Task ToggleLockAsync()
        {
            if (SelectedClosingPeriod is null)
            {
                return;
            }

            var targetState = !SelectedClosingPeriod.IsLocked;
            var closingPeriodId = SelectedClosingPeriod.Id;
            var filterId = SelectedFiscalYearFilter?.FiscalYearId;

            try
            {
                await _closingPeriodService.SetLockStateAsync(closingPeriodId, targetState);
                ToastService.ShowSuccess(targetState
                    ? "ClosingPeriods.Toast.Locked"
                    : "ClosingPeriods.Toast.Unlocked",
                    SelectedClosingPeriod?.Name ?? string.Empty);
                await ReloadAsync(filterId, closingPeriodId);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("ClosingPeriods.Status.ToggleFailed", ex.Message);
                ToastService.ShowError("ClosingPeriods.Toast.ToggleFailed");
            }
        }

        private bool CanAdd() => IsMutationSupported && HasUnlockedFiscalYears;

        private bool CanEdit(ClosingPeriod closingPeriod) => IsMutationSupported && closingPeriod is not null && !closingPeriod.IsLocked;

        private bool CanDelete(ClosingPeriod closingPeriod) => IsMutationSupported && closingPeriod is not null && !closingPeriod.IsLocked;

        private bool CanDeleteData(ClosingPeriod closingPeriod) => IsMutationSupported && closingPeriod is not null && !closingPeriod.IsLocked;

        private bool CanToggleLock() => SelectedClosingPeriod is not null;

        private static bool CanView(ClosingPeriod closingPeriod) => closingPeriod is not null;

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            ToggleLockCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(LockButtonLabel));
        }

        partial void OnFiscalYearsChanging(ObservableCollection<FiscalYear> value)
        {
            if (_fiscalYears != null)
            {
                _fiscalYears.CollectionChanged -= OnFiscalYearsCollectionChanged;
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

        partial void OnSelectedFiscalYearFilterChanged(FiscalYearFilterOption? value)
        {
            if (_isInitializingFilters)
            {
                return;
            }

            ApplyFiscalYearFilter(SelectedClosingPeriod?.Id);
        }

        partial void OnClosingPeriodsChanging(ObservableCollection<ClosingPeriod> value)
        {
            if (_closingPeriods != null)
            {
                _closingPeriods.CollectionChanged -= OnClosingPeriodsCollectionChanged;
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

        private void UpdateFiscalYearFilters(IReadOnlyCollection<FiscalYear> fiscalYears, int? preferredFiscalYearId)
        {
            var displayAll = LocalizationRegistry.Get("ClosingPeriods.Filter.All");
            var options = new List<FiscalYearFilterOption>
            {
                new FiscalYearFilterOption(null, displayAll)
            };

            foreach (var fiscalYear in fiscalYears)
            {
                var displayName = string.IsNullOrWhiteSpace(fiscalYear.Name)
                    ? $"Id={fiscalYear.Id}"
                    : fiscalYear.Name;
                options.Add(new FiscalYearFilterOption(fiscalYear.Id, displayName!));
            }

            _isInitializingFilters = true;
            FiscalYearFilters = new ObservableCollection<FiscalYearFilterOption>(options);
            SelectedFiscalYearFilter = FiscalYearFilters
                .FirstOrDefault(option => option.FiscalYearId == preferredFiscalYearId)
                ?? FiscalYearFilters.FirstOrDefault();
            _isInitializingFilters = false;
        }

        private void ApplyFiscalYearFilter(int? preferredClosingPeriodId)
        {
            if (_isInitializingFilters)
            {
                return;
            }

            var selectedFiscalYearId = SelectedFiscalYearFilter?.FiscalYearId;
            IEnumerable<ClosingPeriod> filtered = _allClosingPeriods;

            if (selectedFiscalYearId.HasValue)
            {
                filtered = filtered.Where(cp => cp.FiscalYearId == selectedFiscalYearId.Value);
            }

            var ordered = filtered.OrderBy(cp => cp.PeriodStart).ToList();
            var targetPeriodId = preferredClosingPeriodId ?? SelectedClosingPeriod?.Id;

            ClosingPeriods = new ObservableCollection<ClosingPeriod>(ordered);

            if (targetPeriodId.HasValue)
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault(cp => cp.Id == targetPeriodId.Value)
                    ?? ClosingPeriods.FirstOrDefault();
            }
            else
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }
        }

        private const bool IsMutationSupported = true;

        public sealed record FiscalYearFilterOption(int? FiscalYearId, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }
    }
}
