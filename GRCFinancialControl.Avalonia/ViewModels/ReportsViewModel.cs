namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        public PlannedVsActualViewModel PlannedVsActual { get; }
        public BacklogViewModel Backlog { get; }
        public FiscalPerformanceViewModel FiscalPerformance { get; }

        public EngagementPerformanceViewModel EngagementPerformance { get; }

        public PapdContributionViewModel PapdContribution { get; }

        public TimeAllocationViewModel TimeAllocation { get; }

        public StrategicKpiViewModel StrategicKpi { get; }

        [ObservableProperty]
        private object? _selectedReport;

        public ReportFilterViewModel Filter { get; }
        public MarginEvolutionViewModel MarginEvolution { get; }

        public ReportsViewModel(ReportFilterViewModel filter, PlannedVsActualViewModel plannedVsActual, BacklogViewModel backlog, FiscalPerformanceViewModel fiscalPerformance, EngagementPerformanceViewModel engagementPerformance, PapdContributionViewModel papdContribution, TimeAllocationViewModel timeAllocation, StrategicKpiViewModel strategicKpi, MarginEvolutionViewModel marginEvolution, IMessenger messenger)
            : base(messenger)
        {
            Filter = filter;
            PlannedVsActual = plannedVsActual;
            Backlog = backlog;
            FiscalPerformance = fiscalPerformance;
            EngagementPerformance = engagementPerformance;
            PapdContribution = papdContribution;
            TimeAllocation = timeAllocation;
            StrategicKpi = strategicKpi;
            MarginEvolution = marginEvolution;

            Messenger.Register<SelectedReportRequestMessage>(this, (r, m) =>
            {
                m.Reply(GetSelectedReportData());
            });
        }

        private IEnumerable<object> GetSelectedReportData()
        {
            return SelectedReport switch
            {
                FiscalPerformanceViewModel vm => new[] { vm },
                EngagementPerformanceViewModel vm => vm.EngagementHealthData,
                PapdContributionViewModel vm => new[] { vm },
                TimeAllocationViewModel vm => new[] { vm },
                StrategicKpiViewModel vm => new[] { vm.KpiData },
                MarginEvolutionViewModel vm => new[] { vm },
                PlannedVsActualViewModel vm => new[] { vm },
                BacklogViewModel vm => new[] { vm },
                _ => Enumerable.Empty<object>()
            };
        }
    }
}