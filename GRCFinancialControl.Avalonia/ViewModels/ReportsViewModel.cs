using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        public PlannedVsActualViewModel PlannedVsActual { get; }
        public BacklogViewModel Backlog { get; }
        public FiscalPerformanceViewModel FiscalPerformance { get; }

        public EngagementPerformanceViewModel EngagementPerformance { get; }

        public PapdContributionViewModel PapdContribution { get; }

        public TimeAllocationViewModel TimeAllocation { get; }

        public StrategicKpiViewModel StrategicKpi { get; }

        public FinancialEvolutionViewModel FinancialEvolution { get; }

        [ObservableProperty]
        private object? _selectedReport;

        public ReportFilterViewModel Filter { get; }

        public ReportsViewModel(ReportFilterViewModel filter, PlannedVsActualViewModel plannedVsActual, BacklogViewModel backlog, FiscalPerformanceViewModel fiscalPerformance, EngagementPerformanceViewModel engagementPerformance, PapdContributionViewModel papdContribution, TimeAllocationViewModel timeAllocation, StrategicKpiViewModel strategicKpi, FinancialEvolutionViewModel financialEvolution, IMessenger messenger)
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
            FinancialEvolution = financialEvolution;

            Messenger.Register<SelectedReportRequestMessage>(this, (r, m) =>
            {
                m.ReportName = GetSelectedReportName();
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
                StrategicKpiViewModel vm when vm.KpiData is not null => new[] { vm.KpiData },
                StrategicKpiViewModel => Enumerable.Empty<object>(),
                PlannedVsActualViewModel vm => new[] { vm },
                FinancialEvolutionViewModel vm when vm.Points.Any() => vm.Points,
                FinancialEvolutionViewModel => Enumerable.Empty<object>(),
                BacklogViewModel vm => new[] { vm },
                _ => Enumerable.Empty<object>()
            };
        }

        private string GetSelectedReportName()
        {
            return SelectedReport switch
            {
                PlannedVsActualViewModel => nameof(PlannedVsActual),
                BacklogViewModel => nameof(Backlog),
                FiscalPerformanceViewModel => nameof(FiscalPerformance),
                EngagementPerformanceViewModel => nameof(EngagementPerformance),
                PapdContributionViewModel => nameof(PapdContribution),
                TimeAllocationViewModel => nameof(TimeAllocation),
                StrategicKpiViewModel => nameof(StrategicKpi),
                FinancialEvolutionViewModel => nameof(FinancialEvolution),
                _ => "Report"
            };
        }
    }
}