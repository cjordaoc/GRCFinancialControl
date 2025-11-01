using System;
using System.Collections.ObjectModel;
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
    public partial class FiscalYearsViewModel : ViewModelBase
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly DialogService _dialogService;
        private readonly ISettingsService _settingsService;
        private readonly LoggingService _loggingService;

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private FiscalYear? _selectedFiscalYear;

        [ObservableProperty]
        private string? _statusMessage;

        public FiscalYearsViewModel(IFiscalYearService fiscalYearService,
                                    DialogService dialogService,
                                    ISettingsService settingsService,
                                    LoggingService loggingService,
                                    IMessenger messenger)
            : base(messenger)
        {
            _fiscalYearService = fiscalYearService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _loggingService = loggingService;
        }

        public override async Task LoadDataAsync()
        {
            var fiscalYears = await _fiscalYearService.GetAllAsync();
            FiscalYears = new ObservableCollection<FiscalYear>(fiscalYears);

            var defaultFiscalYearId = await _settingsService.GetDefaultFiscalYearIdAsync();
            SelectedFiscalYear = FiscalYears.FirstOrDefault(fy => fy.Id == defaultFiscalYearId)
                ?? FiscalYears.FirstOrDefault();
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new FiscalYearEditorViewModel(new FiscalYear(), _fiscalYearService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(FiscalYear fiscalYear)
        {
            if (fiscalYear == null) return;
            var editorViewModel = new FiscalYearEditorViewModel(fiscalYear, _fiscalYearService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanView))]
        private async Task View(FiscalYear fiscalYear)
        {
            if (fiscalYear == null) return;
            var editorViewModel = new FiscalYearEditorViewModel(fiscalYear, _fiscalYearService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(FiscalYear fiscalYear)
        {
            if (fiscalYear == null) return;
            StatusMessage = null;

            try
            {
                await _fiscalYearService.DeleteAsync(fiscalYear.Id);
                ToastService.ShowSuccess("FiscalYears.Toast.DeleteSuccess", fiscalYear.Name);
                Messenger.Send(new RefreshDataMessage());
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowError("FiscalYears.Toast.OperationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                ToastService.ShowError("FiscalYears.Toast.OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(FiscalYear fiscalYear)
        {
            if (fiscalYear is null) return;

            StatusMessage = null;

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Common.Dialog.DeleteData.Title"),
                LocalizationRegistry.Format("Common.Dialog.DeleteData.Message", fiscalYear.Name));
            if (result)
            {
                try
                {
                    await _fiscalYearService.DeleteDataAsync(fiscalYear.Id);
                    ToastService.ShowSuccess("FiscalYears.Toast.DeleteDataSuccess", fiscalYear.Name);
                    Messenger.Send(new RefreshDataMessage());
                }
                catch (InvalidOperationException ex)
                {
                    StatusMessage = ex.Message;
                    ToastService.ShowError("FiscalYears.Toast.OperationFailed", ex.Message);
                }
                catch (Exception ex)
                {
                    StatusMessage = ex.Message;
                    ToastService.ShowError("FiscalYears.Toast.OperationFailed", ex.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanLock))]
        private async Task Lock(FiscalYear fiscalYear)
        {
            if (fiscalYear is null) return;

            StatusMessage = null;

            try
            {
                var user = ResolveCurrentUser();
                var lockedAtUtc = await _fiscalYearService.LockAsync(fiscalYear.Id, user);
                Messenger.Send(new RefreshDataMessage());
                if (lockedAtUtc.HasValue)
                {
                    var lockedAtLocal = DateTime.SpecifyKind(lockedAtUtc.Value, DateTimeKind.Utc).ToLocalTime();
                    StatusMessage = LocalizationRegistry.Format("FiscalYears.Status.Locked", fiscalYear.Name, user, lockedAtLocal);
                    _loggingService.LogInfo($"Fiscal year '{fiscalYear.Name}' locked at {lockedAtUtc.Value:O} by {user}.");
                }
                else
                {
                    StatusMessage = LocalizationRegistry.Format("FiscalYears.Status.AlreadyLocked", fiscalYear.Name);
                    _loggingService.LogWarning($"Attempted to lock fiscal year '{fiscalYear.Name}', but it was already locked or could not be found.");
                }
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanUnlock))]
        private async Task Unlock(FiscalYear fiscalYear)
        {
            if (fiscalYear is null) return;

            StatusMessage = null;

            var user = ResolveCurrentUser();
            var unlockedAtUtc = await _fiscalYearService.UnlockAsync(fiscalYear.Id, user);
            Messenger.Send(new RefreshDataMessage());

            if (unlockedAtUtc.HasValue)
            {
                var unlockedAtLocal = DateTime.SpecifyKind(unlockedAtUtc.Value, DateTimeKind.Utc).ToLocalTime();
                StatusMessage = LocalizationRegistry.Format("FiscalYears.Status.Unlocked", fiscalYear.Name, user, unlockedAtLocal);
                _loggingService.LogInfo($"Fiscal year '{fiscalYear.Name}' unlocked at {unlockedAtUtc.Value:O} by {user}.");
            }
            else
            {
                StatusMessage = LocalizationRegistry.Format("FiscalYears.Status.AlreadyUnlocked", fiscalYear.Name);
                _loggingService.LogWarning($"Attempted to unlock fiscal year '{fiscalYear.Name}', but it was already unlocked or could not be found.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanClose))]
        private async Task Close(FiscalYear fiscalYear)
        {
            if (fiscalYear is null)
            {
                return;
            }

            StatusMessage = null;

            var confirm = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("FiscalYears.Dialog.Close.Title"),
                LocalizationRegistry.Format("FiscalYears.Dialog.Close.Message", fiscalYear.Name));

            if (!confirm)
            {
                return;
            }

            var user = ResolveCurrentUser();

            try
            {
                var result = await _fiscalYearService.CloseAsync(fiscalYear.Id, user);
                var promoted = result.PromotedFiscalYear;

                await _settingsService.SetDefaultFiscalYearIdAsync(promoted?.Id);

                await LoadDataAsync();
                Messenger.Send(new RefreshDataMessage());

                if (promoted != null)
                {
                    StatusMessage = LocalizationRegistry.Format(
                        "FiscalYears.Status.ClosedPromoted",
                        result.ClosedFiscalYear.Name,
                        promoted.Name);
                }
                else
                {
                    StatusMessage = LocalizationRegistry.Format(
                        "FiscalYears.Status.ClosedNoPromotion",
                        result.ClosedFiscalYear.Name);
                }

                var closedAtUtc = result.ClosedFiscalYear.LockedAt ?? DateTime.UtcNow;
                _loggingService.LogInfo(
                    $"Fiscal year '{result.ClosedFiscalYear.Name}' closed at {closedAtUtc:O} by {user}. Promoted: {promoted?.Name ?? "None"}.");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private static bool CanEdit(FiscalYear fiscalYear) => fiscalYear is not null && !fiscalYear.IsLocked;

        private static bool CanDelete(FiscalYear fiscalYear) => fiscalYear is not null && !fiscalYear.IsLocked;

        private static bool CanDeleteData(FiscalYear fiscalYear) => fiscalYear is not null && !fiscalYear.IsLocked;

        private static bool CanLock(FiscalYear fiscalYear) => fiscalYear is not null && !fiscalYear.IsLocked;

        private static bool CanUnlock(FiscalYear fiscalYear) => fiscalYear is not null && fiscalYear.IsLocked;

        private static bool CanClose(FiscalYear fiscalYear) => fiscalYear is not null && !fiscalYear.IsLocked;

        private static bool CanView(FiscalYear fiscalYear) => fiscalYear is not null;

        partial void OnSelectedFiscalYearChanged(FiscalYear? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            LockCommand.NotifyCanExecuteChanged();
            UnlockCommand.NotifyCanExecuteChanged();
            CloseCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

        private static string ResolveCurrentUser()
        {
            var userName = Environment.UserName;
            return string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName;
        }
    }
}
