using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class StrategicKpiViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private StrategicKpiData? _kpiData;

        public StrategicKpiViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public override async Task LoadDataAsync()
        {
            KpiData = await _reportService.GetStrategicKpiDataAsync();
        }
    }
}