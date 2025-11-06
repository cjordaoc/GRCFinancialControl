using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ImportViewModel : ViewModelBase, IRecipient<ApplicationParametersChangedMessage>
    {
        private const string BudgetType = "Budget";
        private const string FullManagementType = "FullManagement";
        private const string AllocationPlanningType = "AllocationPlanning";

        private readonly FilePickerService _filePickerService;
        private readonly IImportService _importService;
        private readonly LoggingService _loggingService;
        private readonly ISettingsService _settingsService;
        private readonly Action<string> _logHandler;

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private string? _fileType;

        [ObservableProperty]
        private bool _hasClosingPeriodSelected;

        public bool CanSelectFullManagement => HasClosingPeriodSelected && !IsImporting;

        public string? ClosingPeriodSelectionWarning => HasClosingPeriodSelected
            ? null
            : LocalizationRegistry.Get("FINC_Import_Warning_SelectClosingPeriod");

        public string? FileTypeDisplayName => FileType switch
        {
            BudgetType => LocalizationRegistry.Get("FINC_Import_FileType_Budget"),
            FullManagementType => LocalizationRegistry.Get("FINC_Import_FileType_FullManagement"),
            AllocationPlanningType => LocalizationRegistry.Get("FINC_Import_FileType_AllocationPlanning"),
            _ => FileType
        };

        public string? SelectedImportTitle => string.IsNullOrWhiteSpace(FileTypeDisplayName)
            ? null
            : LocalizationRegistry.Format("FINC_Import_Section_Selected_TitleFormat", FileTypeDisplayName);

        public ImportViewModel(FilePickerService filePickerService,
                               IImportService importService,
                               LoggingService loggingService,
                               ISettingsService settingsService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _loggingService = loggingService;
            _settingsService = settingsService;
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

            if (string.Equals(fileType, FullManagementType, StringComparison.Ordinal) && !HasClosingPeriodSelected)
            {
                StatusMessage = LocalizationRegistry.Get("FINC_Import_Warning_SelectClosingPeriod");
                return;
            }

            FileType = fileType;
            StatusMessage = null;
            ImportCommand.NotifyCanExecuteChanged();
        }

        private async Task ImportAsync()
        {
            if (IsImporting)
            {
                return;
            }

            if (string.IsNullOrEmpty(FileType))
            {
                return;
            }

            var filePath = await _filePickerService.OpenFileAsync();
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var displayName = FileTypeDisplayName ?? FileType ?? string.Empty;
            _loggingService.LogInfo(LocalizationRegistry.Format("FINC_Import_Status_InProgress", displayName));

            string? resultSummary = null;
            string? backlogSummary = null;
            FullManagementDataImportResult? managementResult = null;

            try
            {
                IsImporting = true;

                switch (FileType)
                {
                    case BudgetType:
                        resultSummary = await Task.Run(() => _importService.ImportBudgetAsync(filePath));
                        break;
                    case FullManagementType:
                        backlogSummary = await Task.Run(() => _importService.ImportFcsRevenueBacklogAsync(filePath));
                        managementResult = await Task.Run(() => _importService.ImportFullManagementDataAsync(filePath));

                        var managementSummary = managementResult?.Summary;
                        if (!string.IsNullOrWhiteSpace(backlogSummary) && !string.IsNullOrWhiteSpace(managementSummary))
                        {
                            resultSummary = string.Join(Environment.NewLine + Environment.NewLine, backlogSummary, managementSummary);
                        }
                        else if (!string.IsNullOrWhiteSpace(backlogSummary))
                        {
                            resultSummary = backlogSummary;
                        }
                        else
                        {
                            resultSummary = managementSummary;
                        }
                        break;
                    case AllocationPlanningType:
                        resultSummary = await Task.Run(() => _importService.ImportAllocationPlanningAsync(filePath));
                        break;
                    default:
                        resultSummary = LocalizationRegistry.Get("FINC_Import_Status_InvalidType");
                        break;
                }

                if (!string.IsNullOrWhiteSpace(resultSummary))
                {
                    _loggingService.LogInfo(resultSummary);
                }

                if (managementResult?.S4MetadataRefreshes > 0)
                {
                    ToastService.ShowSuccess("FINC_Import_Toast_S4MetadataSuccess");
                }
            }
            catch (ImportWarningException warning)
            {
                _loggingService.LogWarning(warning.Message);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(LocalizationRegistry.Format("FINC_Import_Status_Error", ex.Message));
            }
            finally
            {
                IsImporting = false;
            }
        }

        private bool CanImport()
        {
            if (IsImporting || string.IsNullOrEmpty(FileType))
            {
                return false;
            }

            if (string.Equals(FileType, FullManagementType, StringComparison.Ordinal))
            {
                return HasClosingPeriodSelected;
            }

            return true;
        }

        public override async Task LoadDataAsync()
        {
            StatusMessage = null;
            var defaultClosingPeriodId = await _settingsService
                .GetDefaultClosingPeriodIdAsync()
                .ConfigureAwait(false);

            HasClosingPeriodSelected = defaultClosingPeriodId.HasValue;
        }

        partial void OnIsImportingChanged(bool value)
        {
            ImportCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanSelectFullManagement));
        }

        partial void OnFileTypeChanged(string? value)
        {
            OnPropertyChanged(nameof(FileTypeDisplayName));
            OnPropertyChanged(nameof(SelectedImportTitle));
            StatusMessage = null;
            ImportCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasClosingPeriodSelectedChanged(bool value)
        {
            NotifyCommandCanExecute(ImportCommand);
            OnPropertyChanged(nameof(CanSelectFullManagement));
            OnPropertyChanged(nameof(ClosingPeriodSelectionWarning));
        }

        public void Receive(ApplicationParametersChangedMessage message)
        {
            HasClosingPeriodSelected = message.ClosingPeriodId.HasValue;
        }
    }
}
