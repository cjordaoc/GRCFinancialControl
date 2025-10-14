using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class TimeAllocationViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ISeries[] _plannedVsActualSeries = [];

        [ObservableProperty]
        private ISeries[] _burnRateSeries = [];

        [ObservableProperty]
        private Axis[] _allocationAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _burnRateAxes = Array.Empty<Axis>();

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

            // Planned vs ETC-P Hours per Period
            PlannedVsActualSeries = new ISeries[]
            {
                new LineSeries<decimal>
                {
                    Name = "Planned Hours",
                    Values = data.Select(d => d.PlannedHours).ToArray()
                },
                new LineSeries<decimal>
                {
                    Name = "ETC-P Hours",
                    Values = data.Select(d => d.ActualHours).ToArray()
                }
            };

            var labels = data.Select(d => d.ClosingPeriodName).ToArray();
            AllocationAxes = new[]
            {
                new Axis
                {
                    Labels = labels,
                    Name = "Fiscal Year"
                }
            };

            // Burn Rate (% ETC-P / Planned)
            var burnRateValues = data.Select(d => d.PlannedHours > 0 ? (d.ActualHours / d.PlannedHours) * 100 : 0).ToArray();
            BurnRateSeries = new ISeries[]
            {
                new ColumnSeries<decimal>
                {
                    Name = "Burn Rate",
                    Values = burnRateValues,
                    DataLabelsPaint = new SolidColorPaint { Color = SkiaSharp.SKColors.LightGray },
                    DataLabelsSize = 12,
                    DataLabelsFormatter = point => point.Model is decimal value ? $"{value:0.#}%" : string.Empty
                }
            };

            BurnRateAxes = new[]
            {
                new Axis
                {
                    Labels = labels,
                    Name = "Fiscal Year"
                }
            };
        }
    }
}