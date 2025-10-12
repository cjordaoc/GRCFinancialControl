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

        public ImportViewModel(IFilePickerService filePickerService, IImportService importService, IClosingPeriodService closingPeriodService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
            _closingPeriodService = closingPeriodService;

            LoadClosingPeriodsCommand = new AsyncRelayCommand(LoadClosingPeriodsAsync);
            _ = LoadClosingPeriodsCommand.ExecuteAsync(null);
        }

        public IAsyncRelayCommand LoadClosingPeriodsCommand { get; }

        [RelayCommand]
        private async Task ImportBudget()
        {
            var filePath = await _filePickerService.OpenFileAsync();
            if (filePath != null)
            {
                StatusMessage = "Importing budget...";
                StatusMessage = await _importService.ImportBudgetAsync(filePath);
            }
        }

        [RelayCommand]
        private async Task ImportActuals()
        {
            if (SelectedClosingPeriod == null)
            {
                StatusMessage = "Select a closing period before importing margin data.";
                return;
            }

            var filePath = await _filePickerService.OpenFileAsync();
            if (filePath != null)
            {
                StatusMessage = "Importing actuals...";
                StatusMessage = await _importService.ImportActualsAsync(filePath, SelectedClosingPeriod.Id);
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