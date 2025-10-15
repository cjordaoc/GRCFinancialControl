using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        public PapdContributionViewModel PapdContribution { get; }

        public FinancialEvolutionViewModel FinancialEvolution { get; }

        [ObservableProperty]
        private object? _selectedReport;

        private readonly IExportService _exportService;

        public ReportsViewModel(PapdContributionViewModel papdContribution, FinancialEvolutionViewModel financialEvolution, IExportService exportService, IMessenger messenger)
            : base(messenger)
        {
            PapdContribution = papdContribution;
            FinancialEvolution = financialEvolution;
            _exportService = exportService;

            SelectedReport = FinancialEvolution;

            Messenger.Register<SelectedReportRequestMessage>(this, (r, m) =>
            {
                m.ReportName = GetSelectedReportName();
                m.Reply(GetSelectedReportData());
            });
        }

        public override async Task LoadDataAsync()
        {
            await Task.WhenAll(
                PapdContribution.LoadDataAsync(),
                FinancialEvolution.LoadDataAsync());
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportAsync()
        {
            var data = GetSelectedReportData().ToList();
            if (data.Count == 0)
            {
                return;
            }

            await _exportService.ExportToExcelAsync(data, GetSelectedReportName());
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

        private bool CanExport() => SelectedReport is not null;

        partial void OnSelectedReportChanged(object? value)
        {
            ExportCommand.NotifyCanExecuteChanged();
        }
    }
}