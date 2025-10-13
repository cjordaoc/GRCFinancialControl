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
    public partial class MarginEvolutionViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        private ISeries[] _marginEvolutionSeries = [];

        public MarginEvolutionViewModel(IReportService reportService)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            LoadDataCommand.Execute(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public override async Task LoadDataAsync()
        {
            var data = await _reportService.GetMarginEvolutionDataAsync();
            if (data is null || !data.Any()) return;

            var allClosingPeriods = data.SelectMany(d => d.MarginDataPoints.Select(mdp => mdp.ClosingPeriodName)).Distinct().ToList();

            MarginEvolutionSeries = data.Select(d => new LineSeries<decimal>
            {
                Name = d.EngagementName,
                Values = allClosingPeriods.Select(cp => d.MarginDataPoints.FirstOrDefault(mdp => mdp.ClosingPeriodName == cp)?.Margin ?? 0).ToArray()
            }).ToArray();
        }
    }
}