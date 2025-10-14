using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class FinancialEvolutionViewModel : ViewModelBase, IRecipient<ValueChangedMessage<(string? EngagementId, string? EngagementName)>>
    {
        private readonly IReportService _reportService;
        private readonly CultureInfo _ptBr = CultureInfo.GetCultureInfo("pt-BR");
        private readonly List<FinancialEvolutionPoint> _currentPoints = new();
        private string? _selectedEngagementId;

        [ObservableProperty]
        private string? _engagementDisplayName;

        [ObservableProperty]
        private ISeries[] _marginSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _revenueSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _hoursSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _expensesSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ICartesianAxis[] _periodAxes = Array.Empty<ICartesianAxis>();

        [ObservableProperty]
        private ICartesianAxis[] _marginAxes = Array.Empty<ICartesianAxis>();

        [ObservableProperty]
        private ICartesianAxis[] _revenueAxes = Array.Empty<ICartesianAxis>();

        [ObservableProperty]
        private ICartesianAxis[] _hoursAxes = Array.Empty<ICartesianAxis>();

        [ObservableProperty]
        private ICartesianAxis[] _expensesAxes = Array.Empty<ICartesianAxis>();

        public FinancialEvolutionViewModel(IReportService reportService, IMessenger messenger)
            : base(messenger)
        {
            _reportService = reportService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }

        public ObservableCollection<FinancialEvolutionPoint> Points { get; } = new();

        public override async Task LoadDataAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedEngagementId))
            {
                ClearCharts();
                return;
            }

            var data = await _reportService.GetFinancialEvolutionPointsAsync(_selectedEngagementId);

            _currentPoints.Clear();
            Points.Clear();

            foreach (var point in data)
            {
                _currentPoints.Add(point);
                Points.Add(point);
            }

            if (_currentPoints.Count == 0)
            {
                ClearCharts();
                return;
            }

            ConfigureAxes();
            ConfigureSeries();
        }

        public void Receive(ValueChangedMessage<(string? EngagementId, string? EngagementName)> message)
        {
            var (engagementId, engagementName) = message.Value;
            _selectedEngagementId = engagementId;
            EngagementDisplayName = engagementName;
            _ = LoadDataCommand.ExecuteAsync(null);
        }

        private void ClearCharts()
        {
            _currentPoints.Clear();
            Points.Clear();

            MarginSeries = Array.Empty<ISeries>();
            RevenueSeries = Array.Empty<ISeries>();
            HoursSeries = Array.Empty<ISeries>();
            ExpensesSeries = Array.Empty<ISeries>();

            PeriodAxes = Array.Empty<ICartesianAxis>();
            MarginAxes = Array.Empty<ICartesianAxis>();
            RevenueAxes = Array.Empty<ICartesianAxis>();
            HoursAxes = Array.Empty<ICartesianAxis>();
            ExpensesAxes = Array.Empty<ICartesianAxis>();
        }

        private void ConfigureAxes()
        {
            var labels = _currentPoints.Select(p => p.ClosingPeriodId).ToArray();

            PeriodAxes = new ICartesianAxis[]
            {
                new Axis
                {
                    Labels = labels,
                    TextSize = 14
                }
            };

            MarginAxes = new ICartesianAxis[]
            {
                new Axis
                {
                    Labeler = value => $"{value.ToString("N1", _ptBr)} %"
                }
            };

            RevenueAxes = new ICartesianAxis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("C", _ptBr)
                }
            };

            HoursAxes = new ICartesianAxis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("N1", _ptBr)
                }
            };

            ExpensesAxes = new ICartesianAxis[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("C", _ptBr)
                }
            };
        }

        private void ConfigureSeries()
        {
            MarginSeries = new ISeries[]
            {
                CreateLineSeries(p => p.Margin, "Margem (%)", FormatMargin)
            };

            RevenueSeries = new ISeries[]
            {
                CreateLineSeries(p => p.Revenue, "Receita", FormatRevenue)
            };

            HoursSeries = new ISeries[]
            {
                CreateLineSeries(p => p.Hours, "Horas", FormatHours)
            };

            ExpensesSeries = new ISeries[]
            {
                CreateLineSeries(p => p.Expenses, "Despesas", FormatExpenses)
            };
        }

        private LineSeries<double> CreateLineSeries(Func<FinancialEvolutionPoint, decimal?> selector, string name, Func<FinancialEvolutionPoint, string> valueFormatter)
        {
            var values = _currentPoints
                .Select(p => selector(p) is { } value ? (double)value : double.NaN)
                .ToArray();

            return new LineSeries<double>
            {
                Values = values,
                Name = name,
                LineSmoothness = 0,
                GeometrySize = 10,
                Fill = null,
                DataPadding = new LvcPoint(0, 0),
                YToolTipLabelFormatter = point => FormatTooltip(point, name, valueFormatter)
            };
        }

        private string FormatTooltip(ChartPoint<double, CircleGeometry, LabelGeometry> point, string metricLabel, Func<FinancialEvolutionPoint, string> valueFormatter)
        {
            var index = point.Index;

            if (index >= 0 && index < _currentPoints.Count)
            {
                var item = _currentPoints[index];
                var dateText = item.ClosingPeriodDate?.ToString("d", _ptBr) ?? "Sem data";
                return $"{item.ClosingPeriodId} ({dateText})\n{metricLabel}: {valueFormatter(item)}";
            }

            return $"{metricLabel}: {point.Coordinate.PrimaryValue.ToString("N2", _ptBr)}";
        }

        private string FormatMargin(FinancialEvolutionPoint point)
            => point.Margin.HasValue ? $"{point.Margin.Value.ToString("N1", _ptBr)} %" : "Sem dados";

        private string FormatRevenue(FinancialEvolutionPoint point)
            => point.Revenue.HasValue ? point.Revenue.Value.ToString("C", _ptBr) : "Sem dados";

        private string FormatHours(FinancialEvolutionPoint point)
            => point.Hours.HasValue ? $"{point.Hours.Value.ToString("N1", _ptBr)} h" : "Sem dados";

        private string FormatExpenses(FinancialEvolutionPoint point)
            => point.Expenses.HasValue ? point.Expenses.Value.ToString("C", _ptBr) : "Sem dados";
    }
}
