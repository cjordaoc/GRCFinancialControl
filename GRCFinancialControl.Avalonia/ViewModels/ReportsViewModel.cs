using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        public PapdContributionViewModel PapdContribution { get; }

        public FinancialEvolutionViewModel FinancialEvolution { get; }

        [ObservableProperty]
        private object? _selectedReport;

        public ReportFilterViewModel Filter { get; }

        public ReportsViewModel(ReportFilterViewModel filter, PapdContributionViewModel papdContribution, FinancialEvolutionViewModel financialEvolution, IMessenger messenger)
            : base(messenger)
        {
            Filter = filter;
            PapdContribution = papdContribution;
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
                PapdContributionViewModel vm => new[] { vm },
                FinancialEvolutionViewModel vm when vm.Points.Any() => vm.Points,
                FinancialEvolutionViewModel => Enumerable.Empty<object>(),
                _ => Enumerable.Empty<object>()
            };
        }

        private string GetSelectedReportName()
        {
            return SelectedReport switch
            {
                PapdContributionViewModel => nameof(PapdContribution),
                FinancialEvolutionViewModel => nameof(FinancialEvolution),
                _ => "Report"
            };
        }
    }
}