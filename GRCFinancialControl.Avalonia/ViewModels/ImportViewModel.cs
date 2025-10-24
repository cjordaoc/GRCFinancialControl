using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
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
    public partial class ImportViewModel : ViewModelBase, IRecipient<ApplicationParametersChangedMessage>
    {
        private const string BudgetType = "Budget";
        private const string ActualsType = "Actuals";
        private const string FullManagementType = "FullManagement";
        private const string AllocationPlanningType = "AllocationPlanning";

        private readonly FilePickerService _filePickerService;
        private readonly IImportService _importService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly ISettingsService _settingsService;
        private readonly LoggingService _loggingService;
        private readonly Action<string> _logHandler;
        private int? _pendingClosingPeriodId;

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private string? _fileType;

        public string? FileTypeDisplayName => FileType switch
        {
            BudgetType => LocalizationRegistry.Get("Import.FileType.Budget"),
            ActualsType => LocalizationRegistry.Get("Import.FileType.Actuals"),
            FullManagementType => LocalizationRegistry.Get("Import.FileType.FullManagement"),
            AllocationPlanningType => LocalizationRegistry.Get("Import.FileType.AllocationPlanning"),
            _ => FileType
        };

        public string? SelectedImportTitle => string.IsNullOrWhiteSpace(FileTypeDisplayName)
            ? null
            : LocalizationRegistry.Format("Import.Section.Selected.TitleFormat", FileTypeDisplayName);

        public bool ShouldShowClosingPeriodSummary => RequiresClosingPeriodSelection && SelectedClosingPeriod is not null;

        public string? SelectedClosingPeriodSummary
        {
            get
            {
                if (SelectedClosingPeriod is null)
                {
                    return null;
                }

                return LocalizationRegistry.Format(
                    "Import.Status.SelectedClosingPeriod",
                    SelectedClosingPeriod.Name,
                    SelectedClosingPeriod.PeriodStart,
                    SelectedClosingPeriod.PeriodEnd);
            }
        }

        public ImportViewModel(FilePickerService filePickerService,
                               IImportService importService,
                               IClosingPeriodService closingPeriodService,
                               ISettingsService settingsService,
                               LoggingService loggingService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _closingPeriodService = closingPeriodService;
            _settingsService = settingsService;
            _loggingService = loggingService;
            _logHandler = message =>
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    StatusMessage = message;
                }
                else
                {
                    Dispatcher.UIThread.Post(() => StatusMessage = message);
                }
            };
            _loggingService.OnLogMessage += _logHandler;

            SetImportTypeCommand = new RelayCommand<string>(SetImportType);
            ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        }

        public IRelayCommand SetImportTypeCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }

        private void SetImportType(string? fileType)
        {
            if (IsImporting)
            {
                return;
            }

            FileType = fileType;
            UpdateClosingPeriodStatusMessage();
            ImportCommand.NotifyCanExecuteChanged();
        }

        private async Task ImportAsync()
        {
            if (IsImporting)
            {
                return;
            }

            if (string.IsNullOrEmpty(FileType)) return;

            if (FileType == ActualsType && SelectedClosingPeriod == null)
            {
                StatusMessage = LocalizationRegistry.Get("Import.Validation.ClosingPeriodRequired");
                return;
            }

            var filePath = await _filePickerService.OpenFileAsync();
            if (string.IsNullOrEmpty(filePath)) return;

            var displayName = FileTypeDisplayName ?? FileType ?? string.Empty;
            _loggingService.LogInfo(LocalizationRegistry.Format("Import.Status.InProgress", displayName));
            try
            {
                IsImporting = true;

                string result;
                if (FileType == BudgetType)
                {
                    result = await Task.Run(() => _importService.ImportBudgetAsync(filePath));
                }
                else if (FileType == ActualsType)
                {
                    var closingPeriodId = SelectedClosingPeriod!.Id;
                    result = await Task.Run(() => _importService.ImportActualsAsync(filePath, closingPeriodId));
                }
                else if (FileType == FullManagementType)
                {
                    result = await Task.Run(() => _importService.ImportFullManagementDataAsync(filePath));
                }
                else if (FileType == AllocationPlanningType)
                {
                    result = await Task.Run(() => _importService.ImportAllocationPlanningAsync(filePath));
                }
                else
                {
                    result = LocalizationRegistry.Get("Import.Status.InvalidType");
                }
                _loggingService.LogInfo(result);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(LocalizationRegistry.Format("Import.Status.Error", ex.Message));
            }
            finally
            {
                IsImporting = false;
            }
        }

        private bool CanImport()
        {
            if (IsImporting)
            {
                return false;
            }

            if (string.IsNullOrEmpty(FileType))
            {
                return false;
            }

            if (FileType == ActualsType)
            {
                return SelectedClosingPeriod is not null;
            }

            return true;
        }

        public override async Task LoadDataAsync()
        {
            var periods = await _closingPeriodService.GetAllAsync();
            var unlockedPeriods = periods
                .Where(p => !(p.FiscalYear?.IsLocked ?? false))
                .OrderBy(p => p.PeriodStart)
                .ToList();

            ClosingPeriods = new ObservableCollection<ClosingPeriod>(unlockedPeriods);

            var preferredClosingPeriodId = _pendingClosingPeriodId
                ?? await _settingsService.GetDefaultClosingPeriodIdAsync().ConfigureAwait(false);
            _pendingClosingPeriodId = null;

            if (preferredClosingPeriodId.HasValue)
            {
                var matched = ClosingPeriods.FirstOrDefault(p => p.Id == preferredClosingPeriodId.Value);
                SelectedClosingPeriod = matched ?? ClosingPeriods.FirstOrDefault();
            }
            else if (SelectedClosingPeriod == null || ClosingPeriods.All(p => p.Id != SelectedClosingPeriod.Id))
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }

            UpdateClosingPeriodStatusMessage();
        }

        public bool HasUnlockedClosingPeriods => ClosingPeriods.Count > 0;

        public bool RequiresClosingPeriodSelection => string.Equals(FileType, ActualsType, StringComparison.Ordinal);

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
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedClosingPeriods));
            UpdateClosingPeriodStatusMessage();
            OnPropertyChanged(nameof(ShouldShowClosingPeriodSummary));
            OnPropertyChanged(nameof(SelectedClosingPeriodSummary));
        }

        private void OnClosingPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedClosingPeriods));
            UpdateClosingPeriodStatusMessage();
            OnPropertyChanged(nameof(ShouldShowClosingPeriodSummary));
            OnPropertyChanged(nameof(SelectedClosingPeriodSummary));
        }

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            ImportCommand.NotifyCanExecuteChanged();
            UpdateClosingPeriodStatusMessage();
            OnPropertyChanged(nameof(ShouldShowClosingPeriodSummary));
            OnPropertyChanged(nameof(SelectedClosingPeriodSummary));
        }

        partial void OnIsImportingChanged(bool value)
        {
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnFileTypeChanged(string? value)
        {
            OnPropertyChanged(nameof(FileTypeDisplayName));
            OnPropertyChanged(nameof(SelectedImportTitle));
            OnPropertyChanged(nameof(RequiresClosingPeriodSelection));
            OnPropertyChanged(nameof(ShouldShowClosingPeriodSummary));
            OnPropertyChanged(nameof(SelectedClosingPeriodSummary));
            UpdateClosingPeriodStatusMessage();
            ImportCommand.NotifyCanExecuteChanged();
        }

        private void UpdateClosingPeriodStatusMessage()
        {
            if (!RequiresClosingPeriodSelection)
            {
                var lockedMessage = LocalizationRegistry.Get("Import.Status.LockedClosingPeriods");
                var requiredMessage = LocalizationRegistry.Get("Import.Validation.ClosingPeriodRequired");
                if (string.Equals(StatusMessage, lockedMessage, StringComparison.Ordinal) ||
                    string.Equals(StatusMessage, requiredMessage, StringComparison.Ordinal))
                {
                    StatusMessage = null;
                }
                return;
            }

            if (!HasUnlockedClosingPeriods)
            {
                StatusMessage = LocalizationRegistry.Get("Import.Status.LockedClosingPeriods");
                return;
            }

            if (SelectedClosingPeriod is null)
            {
                StatusMessage = LocalizationRegistry.Get("Import.Validation.ClosingPeriodRequired");
                return;
            }

            var locked = LocalizationRegistry.Get("Import.Status.LockedClosingPeriods");
            var required = LocalizationRegistry.Get("Import.Validation.ClosingPeriodRequired");
            if (string.Equals(StatusMessage, locked, StringComparison.Ordinal) ||
                string.Equals(StatusMessage, required, StringComparison.Ordinal))
            {
                StatusMessage = null;
            }
        }

        public void Receive(ApplicationParametersChangedMessage message)
        {
            if (message is null)
            {
                return;
            }

            _pendingClosingPeriodId = message.ClosingPeriodId;

            if (message.ClosingPeriodId is null)
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
                return;
            }

            var match = ClosingPeriods.FirstOrDefault(p => p.Id == message.ClosingPeriodId.Value);
            if (match != null)
            {
                SelectedClosingPeriod = match;
            }
            else
            {
                _ = LoadDataAsync();
            }
        }
    }
}