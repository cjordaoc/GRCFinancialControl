using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ImportViewModel : ViewModelBase
    {
        private const string LockedClosingPeriodsMessage = "All closing periods belong to locked fiscal years. Unlock a fiscal year to import ETC-P data.";

        private readonly IFilePickerService _filePickerService;
        private readonly IImportService _importService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly ILoggingService _loggingService;
        private readonly DataBackendOptions _dataBackendOptions;

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
            "Actuals" => "ETC-P",
            "FcsBacklog" => "FCS Revenue Backlog",
            "FullManagement" => "Full Management Data",
            _ => FileType
        };

        public ImportViewModel(IFilePickerService filePickerService, IImportService importService, IClosingPeriodService closingPeriodService, ILoggingService loggingService, DataBackendOptions dataBackendOptions)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _closingPeriodService = closingPeriodService;
            _loggingService = loggingService;
            _dataBackendOptions = dataBackendOptions;
            _loggingService.OnLogMessage += (message) => StatusMessage = message;

            SetImportTypeCommand = new RelayCommand<string>(SetImportType);
            ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        }

        public IRelayCommand SetImportTypeCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }

        private void SetImportType(string? fileType)
        {
            FileType = fileType;
            if (RequiresClosingPeriodSelection && !HasUnlockedClosingPeriods)
            {
                StatusMessage = LockedClosingPeriodsMessage;
            }
            else
            {
                StatusMessage = null;
            }
            ImportCommand.NotifyCanExecuteChanged();
        }

        private async Task ImportAsync()
        {
            if (string.IsNullOrEmpty(FileType)) return;

            if (FileType == "Actuals" && SelectedClosingPeriod == null)
            {
                StatusMessage = "Please select a closing period before importing ETC-P data.";
                return;
            }

            var filePath = await _filePickerService.OpenFileAsync();
            if (string.IsNullOrEmpty(filePath)) return;

            var displayName = FileTypeDisplayName ?? FileType ?? string.Empty;
            _loggingService.LogInfo($"Importing {displayName} data...");
            try
            {
                string result;
                if (FileType == "Budget")
                {
                    result = await _importService.ImportBudgetAsync(filePath);
                }
                else if (FileType == "Actuals")
                {
                    result = await _importService.ImportActualsAsync(filePath, SelectedClosingPeriod!.Id);
                }
                else if (FileType == "FcsBacklog")
                {
                    result = await _importService.ImportFcsRevenueBacklogAsync(filePath);
                }
                else if (FileType == "FullManagement")
                {
                    result = await _importService.ImportFullManagementDataAsync(filePath);
                }
                else
                {
                    result = "Invalid import type selected.";
                }
                _loggingService.LogInfo(result);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"An error occurred during import: {ex.Message}");
            }
        }

        private bool CanImport()
        {
            if (!IsImportSupported || string.IsNullOrEmpty(FileType))
            {
                return false;
            }

            if (FileType == "Actuals")
            {
                return SelectedClosingPeriod is not null;
            }

            return true;
        }

        public override async Task LoadDataAsync()
        {
            if (!IsImportSupported)
            {
                StatusMessage = "Imports are not available when the Dataverse backend is active.";
            }

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

            if (FileType == "Actuals" && ClosingPeriods.Count == 0)
            {
                StatusMessage ??= LockedClosingPeriodsMessage;
            }
            else if (FileType == "Actuals" && string.Equals(StatusMessage, LockedClosingPeriodsMessage, StringComparison.Ordinal))
            {
                StatusMessage = null;
            }
        }

        public bool HasUnlockedClosingPeriods => ClosingPeriods.Count > 0;

        public bool RequiresClosingPeriodSelection => string.Equals(FileType, "Actuals", StringComparison.Ordinal);

        public bool IsClosingPeriodSelectionUnavailable => RequiresClosingPeriodSelection && !HasUnlockedClosingPeriods;

        private bool IsImportSupported => _dataBackendOptions.Backend == DataBackend.MySql;

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
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedClosingPeriods));
            OnPropertyChanged(nameof(IsClosingPeriodSelectionUnavailable));
        }

        private void OnClosingPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnlockedClosingPeriods));
            OnPropertyChanged(nameof(IsClosingPeriodSelectionUnavailable));
        }

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnFileTypeChanged(string? value)
        {
            OnPropertyChanged(nameof(FileTypeDisplayName));
            OnPropertyChanged(nameof(RequiresClosingPeriodSelection));
            OnPropertyChanged(nameof(IsClosingPeriodSelectionUnavailable));
            ImportCommand.NotifyCanExecuteChanged();
        }
    }
}