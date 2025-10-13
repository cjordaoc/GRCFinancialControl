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
    public partial class PapdContributionViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ISeries[] _revenueContributionSeries = [];

        [ObservableProperty]
        private ISeries[] _hoursWorkedSeries = [];

        public PapdContributionViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public override async Task LoadDataAsync()
        {
            var data = await _reportService.GetPapdContributionDataAsync();
            if (data is null || !data.Any()) return;

            // Revenue Contribution by PAPD
            RevenueContributionSeries = data.Select(d => new PieSeries<decimal>
            {
                Name = d.PapdName,
                Values = new[] { d.RevenueContribution }
            }).ToArray();

            // Hours Worked by PAPD (Over Time)
            var allClosingPeriods = data.SelectMany(d => d.HoursWorked.Select(hw => hw.ClosingPeriodName)).Distinct().ToList();
            HoursWorkedSeries = data.Select(d => new LineSeries<decimal>
            {
                Name = d.PapdName,
                Values = allClosingPeriods.Select(cp => d.HoursWorked.FirstOrDefault(hw => hw.ClosingPeriodName == cp)?.Hours ?? 0).ToArray()
            }).ToArray();
        }
    }
}