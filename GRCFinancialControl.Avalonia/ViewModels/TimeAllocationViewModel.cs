using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class TimeAllocationViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ISeries[] _plannedVsActualSeries = [];

        [ObservableProperty]
        private ISeries[] _burnRateSeries = [];

        public TimeAllocationViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public override async Task LoadDataAsync()
        {
            var data = await _reportService.GetTimeAllocationDataAsync();
            if (data is null || !data.Any()) return;

            // Planned vs Actual Hours per Period
            PlannedVsActualSeries = new ISeries[]
            {
                new LineSeries<decimal>
                {
                    Name = "Planned Hours",
                    Values = data.Select(d => d.PlannedHours).ToArray()
                },
                new LineSeries<decimal>
                {
                    Name = "Actual Hours",
                    Values = data.Select(d => d.ActualHours).ToArray()
                }
            };

            // Burn Rate (% Actual / Planned)
            BurnRateSeries = new ISeries[]
            {
                new ColumnSeries<decimal>
                {
                    Name = "Burn Rate",
                    Values = data.Select(d => d.PlannedHours > 0 ? (d.ActualHours / d.PlannedHours) * 100 : 0).ToArray()
                }
            };
        }
    }
}