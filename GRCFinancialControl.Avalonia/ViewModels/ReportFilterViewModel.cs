using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ReportFilterViewModel : ViewModelBase
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IPapdService _papdService;
        private readonly IEngagementService _engagementService;

        [ObservableProperty]
        private ObservableCollection<string> _fiscalYears = new();

        [ObservableProperty]
        private string? _selectedFiscalYear;

        [ObservableProperty]
        private ObservableCollection<string> _papds = new();

        [ObservableProperty]
        private string? _selectedPapd;

        [ObservableProperty]
        private ObservableCollection<string> _engagements = new();

        [ObservableProperty]
        private string? _selectedEngagement;

        public ReportFilterViewModel(IFiscalYearService fiscalYearService, IPapdService papdService, IEngagementService engagementService)
        {
            _fiscalYearService = fiscalYearService;
            _papdService = papdService;
            _engagementService = engagementService;
            LoadFiltersCommand = new AsyncRelayCommand(LoadFiltersAsync);
            LoadFiltersCommand.Execute(null);
        }

        private readonly IExportService _exportService;
        private readonly IMessenger _messenger;

        public IAsyncRelayCommand LoadFiltersCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }

        public ReportFilterViewModel(IFiscalYearService fiscalYearService, IPapdService papdService, IEngagementService engagementService, IExportService exportService, IMessenger messenger)
        {
            _fiscalYearService = fiscalYearService;
            _papdService = papdService;
            _engagementService = engagementService;
            _exportService = exportService;
            _messenger = messenger;
            LoadFiltersCommand = new AsyncRelayCommand(LoadFiltersAsync);
            ExportCommand = new AsyncRelayCommand(ExportAsync);
            LoadFiltersCommand.Execute(null);
        }

        private async Task LoadFiltersAsync()
        {
            var fiscalYears = await _fiscalYearService.GetAllAsync();
            FiscalYears = new ObservableCollection<string>(fiscalYears.Select(fy => fy.Name));

            var papds = await _papdService.GetAllAsync();
            Papds = new ObservableCollection<string>(papds.Select(p => p.Name));

            var engagements = await _engagementService.GetAllAsync();
            Engagements = new ObservableCollection<string>(engagements.Select(e => e.Description));
        }

        private async Task ExportAsync()
        {
            var selectedReport = _messenger.Send<SelectedReportRequestMessage>();
            if (selectedReport.Response != null)
            {
                await _exportService.ExportToCsvAsync(selectedReport.Response, "report.csv");
            }
        }
    }
}