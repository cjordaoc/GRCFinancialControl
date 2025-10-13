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
    public partial class EngagementPerformanceViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ISeries[] _hoursConsumedSeries = [];

        [ObservableProperty]
        private ISeries[] _hoursDistributionSeries = [];

        [ObservableProperty]
        private ObservableCollection<EngagementPerformanceData> _engagementHealthData = new();

        public EngagementPerformanceViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public override async Task LoadDataAsync()
        {
            var data = await _reportService.GetEngagementPerformanceDataAsync();
            if (data is null || !data.Any()) return;

            EngagementHealthData = new ObservableCollection<EngagementPerformanceData>(data);

            // Hours Consumed vs Planned
            HoursConsumedSeries = new ISeries[]
            {
                new ColumnSeries<decimal>
                {
                    Name = "Planned Hours",
                    Values = data.Select(d => d.InitialHoursBudget).ToArray()
                },
                new ColumnSeries<decimal>
                {
                    Name = "Actual Hours",
                    Values = data.Select(d => d.ActualHours).ToArray()
                }
            };

            // Hours Distribution by Rank
            var allRanks = data.SelectMany(d => d.RankBudgets.Select(rb => rb.RankName)).Distinct().ToList();
            HoursDistributionSeries = allRanks.Select(rank => new StackedColumnSeries<decimal>
            {
                Name = rank,
                Values = data.Select(d => d.RankBudgets.FirstOrDefault(rb => rb.RankName == rank)?.Hours ?? 0).ToArray()
            }).ToArray();
        }
    }
}