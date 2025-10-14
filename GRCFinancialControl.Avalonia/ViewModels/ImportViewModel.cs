using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ImportViewModel : ViewModelBase
    {
        private readonly IFilePickerService _filePickerService;
        private readonly IImportService _importService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly ILoggingService _loggingService;

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
            _ => FileType
        };

        public ImportViewModel(IFilePickerService filePickerService, IImportService importService, IClosingPeriodService closingPeriodService, ILoggingService loggingService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _closingPeriodService = closingPeriodService;
            _loggingService = loggingService;
            _loggingService.OnLogMessage += (message) => StatusMessage = message;

            SetImportTypeCommand = new RelayCommand<string>(SetImportType);
            ImportCommand = new AsyncRelayCommand(ImportAsync);
        }

        public IRelayCommand SetImportTypeCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }

        partial void OnFileTypeChanged(string? value)
        {
            OnPropertyChanged(nameof(FileTypeDisplayName));
        }

        private void SetImportType(string? fileType)
        {
            FileType = fileType;
            StatusMessage = null;
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

        public override async Task LoadDataAsync()
        {
            var periods = await _closingPeriodService.GetAllAsync();
            ClosingPeriods = new ObservableCollection<ClosingPeriod>(periods);

            if (SelectedClosingPeriod == null)
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }
            else if (ClosingPeriods.All(p => p.Id != SelectedClosingPeriod.Id))
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
            }
        }
    }
}