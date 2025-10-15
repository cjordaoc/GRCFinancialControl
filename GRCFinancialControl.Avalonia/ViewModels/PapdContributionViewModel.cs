using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdContributionViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;
        private readonly CultureInfo _cultureInfo = CultureInfo.GetCultureInfo("pt-BR");

        [ObservableProperty]
        private ISeries[] _revenueContributionSeries = [];

        [ObservableProperty]
        private ISeries[] _hoursWorkedSeries = [];

        [ObservableProperty]
        private ICartesianAxis[] _hoursXAxes = Array.Empty<ICartesianAxis>();

        [ObservableProperty]
        private ICartesianAxis[] _hoursYAxes = Array.Empty<ICartesianAxis>();

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

            if (data is null || data.Count == 0)
            {
                RevenueContributionSeries = Array.Empty<ISeries>();
                HoursWorkedSeries = Array.Empty<ISeries>();
                HoursXAxes = Array.Empty<ICartesianAxis>();
                HoursYAxes = Array.Empty<ICartesianAxis>();
                return;
            }

            var periodSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allClosingPeriods = new List<string>();

            foreach (var series in data)
            {
                foreach (var point in series.HoursWorked)
                {
                    if (string.IsNullOrWhiteSpace(point.ClosingPeriodName))
                    {
                        continue;
                    }

                    if (periodSet.Add(point.ClosingPeriodName))
                    {
                        allClosingPeriods.Add(point.ClosingPeriodName);
                    }
                }
            }

            RevenueContributionSeries = data
                .Select(d => new PieSeries<double>
                {
                    Name = d.PapdName,
                    Values = new[] { (double)d.RevenueContribution }
                })
                .Cast<ISeries>()
                .ToArray();

            HoursWorkedSeries = data
                .Select(d => new LineSeries<double>
                {
                    Name = d.PapdName,
                    Values = allClosingPeriods
                        .Select(cp =>
                            (double)(d.HoursWorked
                                .FirstOrDefault(hw => string.Equals(hw.ClosingPeriodName, cp, StringComparison.OrdinalIgnoreCase))?.Hours ?? 0m))
                        .ToArray(),
                    Fill = null,
                    GeometrySize = 8,
                    LineSmoothness = 0
                })
                .Cast<ISeries>()
                .ToArray();

            HoursXAxes = allClosingPeriods.Count == 0
                ? Array.Empty<ICartesianAxis>()
                : new ICartesianAxis[]
                {
                    new Axis
                    {
                        Labels = allClosingPeriods.ToArray(),
                        LabelsRotation = 0
                    }
                };

            HoursYAxes = new ICartesianAxis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("N1", _cultureInfo)
                }
            };
        }
    }
}