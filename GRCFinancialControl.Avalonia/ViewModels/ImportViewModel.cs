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

        public string? FileTypeDisplayName => FileType switch
        {
            BudgetType => LocalizationRegistry.Get("Import.FileType.Budget"),
            FullManagementType => LocalizationRegistry.Get("Import.FileType.FullManagement"),
            AllocationPlanningType => LocalizationRegistry.Get("Import.FileType.AllocationPlanning"),
            _ => FileType
        };

        public string? SelectedImportTitle => string.IsNullOrWhiteSpace(FileTypeDisplayName)
            ? null
            : LocalizationRegistry.Format("Import.Section.Selected.TitleFormat", FileTypeDisplayName);

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
            _loggingService.LogInfo(LocalizationRegistry.Format("Import.Status.InProgress", displayName));

            string? result = null;

            try
            {
                IsImporting = true;

                result = FileType switch
                {
                    BudgetType => await Task.Run(() => _importService.ImportBudgetAsync(filePath)),
                    FullManagementType => await Task.Run(() => _importService.ImportFullManagementDataAsync(filePath)),
                    AllocationPlanningType => await Task.Run(() => _importService.ImportAllocationPlanningAsync(filePath)),
                    _ => LocalizationRegistry.Get("Import.Status.InvalidType")
                };

                if (!string.IsNullOrWhiteSpace(result))
                {
                    _loggingService.LogInfo(result);
                }
            }
            catch (ImportWarningException warning)
            {
                _loggingService.LogWarning(warning.Message);
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
        }

        public void Receive(ApplicationParametersChangedMessage message)
        {
            HasClosingPeriodSelected = message.ClosingPeriodId.HasValue;
        }
    }
}
