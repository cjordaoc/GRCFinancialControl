using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class BacklogViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ObservableCollection<BacklogData> _reportData = new();

        public BacklogViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadReportDataCommand = new AsyncRelayCommand(LoadReportDataAsync);
        }

        public IAsyncRelayCommand LoadReportDataCommand { get; }

        private async Task LoadReportDataAsync()
        {
            var data = await _reportService.GetBacklogDataAsync();
            ReportData = new ObservableCollection<BacklogData>(data);
        }
    }
}