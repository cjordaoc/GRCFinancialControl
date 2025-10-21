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
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ImportViewModel : ViewModelBase
    {
        private const string BudgetType = "Budget";
        private const string ActualsType = "Actuals";
        private const string FullManagementType = "FullManagement";

        private readonly IFilePickerService _filePickerService;
        private readonly IImportService _importService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly ILoggingService _loggingService;
        private readonly Action<string> _logHandler;

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
            _ => FileType
        };

        public string? SelectedImportTitle => string.IsNullOrWhiteSpace(FileTypeDisplayName)
            ? null
            : LocalizationRegistry.Format("Import.Section.Selected.TitleFormat", FileTypeDisplayName);

        public bool CanModifyClosingPeriod => HasUnlockedClosingPeriods && !IsImporting;

        public ImportViewModel(IFilePickerService filePickerService, IImportService importService, IClosingPeriodService closingPeriodService, ILoggingService loggingService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _closingPeriodService = closingPeriodService;
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
            if (RequiresClosingPeriodSelection && !HasUnlockedClosingPeriods)
            {
                StatusMessage = LocalizationRegistry.Get("Import.Status.LockedClosingPeriods");
            }
            else
            {
                StatusMessage = null;
            }
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

            if (SelectedClosingPeriod == null)
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }
            else if (ClosingPeriods.All(p => p.Id != SelectedClosingPeriod.Id))
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }

            var lockedMessage = LocalizationRegistry.Get("Import.Status.LockedClosingPeriods");

            if (FileType == ActualsType && ClosingPeriods.Count == 0)
            {
                StatusMessage ??= lockedMessage;
            }
            else if (FileType == ActualsType && string.Equals(StatusMessage, lockedMessage, StringComparison.Ordinal))
            {
                StatusMessage = null;
            }
        }

        public bool HasUnlockedClosingPeriods => ClosingPeriods.Count > 0;

        public bool RequiresClosingPeriodSelection => string.Equals(FileType, ActualsType, StringComparison.Ordinal);

        public bool IsClosingPeriodSelectionUnavailable => RequiresClosingPeriodSelection && !HasUnlockedClosingPeriods;

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
            OnPropertyChanged(nameof(IsClosingPeriodSelectionUnavailable));
            OnPropertyChanged(nameof(CanModifyClosingPeriod));
        }

        private void OnClosingPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedClosingPeriods));
            OnPropertyChanged(nameof(IsClosingPeriodSelectionUnavailable));
            OnPropertyChanged(nameof(CanModifyClosingPeriod));
        }

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsImportingChanged(bool value)
        {
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanModifyClosingPeriod));
        }

        partial void OnFileTypeChanged(string? value)
        {
            OnPropertyChanged(nameof(FileTypeDisplayName));
            OnPropertyChanged(nameof(SelectedImportTitle));
            OnPropertyChanged(nameof(RequiresClosingPeriodSelection));
            OnPropertyChanged(nameof(IsClosingPeriodSelectionUnavailable));
            ImportCommand.NotifyCanExecuteChanged();
        }
    }
}