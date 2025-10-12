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

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private string? _fileType;

        public ImportViewModel(IFilePickerService filePickerService, IImportService importService, IClosingPeriodService closingPeriodService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _closingPeriodService = closingPeriodService;

            LoadClosingPeriodsCommand = new AsyncRelayCommand(LoadClosingPeriodsAsync);
            SetImportTypeCommand = new RelayCommand<string>(SetImportType);
            ImportCommand = new AsyncRelayCommand(ImportAsync);
            _ = LoadClosingPeriodsCommand.ExecuteAsync(null);
        }

        public IAsyncRelayCommand LoadClosingPeriodsCommand { get; }
        public IRelayCommand SetImportTypeCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }

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
                StatusMessage = "Please select a closing period before importing margin data.";
                return;
            }

            var filePath = await _filePickerService.OpenFileAsync();
            if (string.IsNullOrEmpty(filePath)) return;

            StatusMessage = $"Importing {FileType.ToLower()} data...";
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
                StatusMessage = result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"An error occurred during import: {ex.Message}";
            }
        }

        private async Task LoadClosingPeriodsAsync()
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