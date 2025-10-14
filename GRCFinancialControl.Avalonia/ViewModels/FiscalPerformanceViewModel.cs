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
using SkiaSharp;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class FiscalPerformanceViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ISeries[] _salesSeries = [];

        [ObservableProperty]
        private ISeries[] _revenueSeries = [];

        [ObservableProperty]
        private ISeries[] _papdContributionSeries = [];

        [ObservableProperty]
        private ISeries[] _plannedVsActualHoursSeries = [];

        public FiscalPerformanceViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public override async Task LoadDataAsync()
        {
            var data = await _reportService.GetFiscalPerformanceDataAsync();
            if (data is null || !data.Any()) return;

            // Sales Progress
            SalesSeries = data.Select(d => new ColumnSeries<decimal>
            {
                Name = d.FiscalYearName,
                Values = new[] { d.TotalRevenue },
                MaxBarWidth = 50,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Middle,
                DataLabelsFormatter = point => point.Model.ToString("C")
            }).ToArray();

            // Revenue Progress
            RevenueSeries = data.Select(d => new ColumnSeries<decimal>
            {
                Name = d.FiscalYearName,
                Values = new[] { d.TotalRevenue },
                MaxBarWidth = 50,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Middle,
                DataLabelsFormatter = point => point.Model.ToString("C")
            }).ToArray();

            // PAPD Contribution
            PapdContributionSeries = data
                .SelectMany(d => d.PapdContributions)
                .GroupBy(pc => pc.PapdName)
                .Select(g => new PieSeries<decimal>
                {
                    Name = g.Key,
                    Values = new[] { g.Sum(pc => pc.Revenue) }
                }).ToArray();

            // Planned vs ETC-P Hours
            PlannedVsActualHoursSeries = new ISeries[]
            {
                new ColumnSeries<decimal>
                {
                    Name = "Planned Hours",
                    Values = data.Select(d => d.TotalPlannedHours).ToArray()
                },
                new ColumnSeries<decimal>
                {
                    Name = "ETC-P Hours",
                    Values = data.Select(d => d.TotalActualHours).ToArray()
                }
            };
        }
    }
}