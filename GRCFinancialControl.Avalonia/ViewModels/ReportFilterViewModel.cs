using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
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

        private readonly IExportService _exportService;

        public IAsyncRelayCommand LoadFiltersCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }

        public ReportFilterViewModel(IFiscalYearService fiscalYearService, IPapdService papdService, IEngagementService engagementService, IExportService exportService, IMessenger messenger)
            : base(messenger)
        {
            _fiscalYearService = fiscalYearService;
            _papdService = papdService;
            _engagementService = engagementService;
            _exportService = exportService;
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
            var selectedReport = Messenger.Send<SelectedReportRequestMessage>();
            if (selectedReport.Response is null)
            {
                return;
            }

            var data = selectedReport.Response.ToList();
            await _exportService.ExportToExcelAsync(data, selectedReport.ReportName);
        }
    }
}