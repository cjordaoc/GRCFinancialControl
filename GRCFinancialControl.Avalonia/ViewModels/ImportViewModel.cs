using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ImportViewModel : ViewModelBase
    {
        private readonly IFilePickerService _filePickerService;
        private readonly IImportService _importService;

        [ObservableProperty]
        private string? _statusMessage;

        public ImportViewModel(IFilePickerService filePickerService, IImportService importService)
        {
            _filePickerService = filePickerService;
            _importService = importService;
        }

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
            var filePath = await _filePickerService.OpenFileAsync();
            if (filePath != null)
            {
                StatusMessage = "Importing actuals...";
                StatusMessage = await _importService.ImportActualsAsync(filePath);
            }
        }
    }
}