using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
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
        private ObservableCollection<EngagementFilterOption> _engagements = new();

        [ObservableProperty]
        private EngagementFilterOption? _selectedEngagement;

        private readonly IExportService _exportService;

        public IAsyncRelayCommand LoadFiltersCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }

        public ReportFilterViewModel(
            IFiscalYearService fiscalYearService,
            IPapdService papdService,
            IEngagementService engagementService,
            IExportService exportService,
            IMessenger messenger)
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

            var previousSelectionId = SelectedEngagement?.EngagementId;

            var engagements = await _engagementService.GetAllAsync();
            var options = engagements
                .OrderBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EngagementFilterOption(e.EngagementId, e.Description))
                .ToList();

            Engagements = new ObservableCollection<EngagementFilterOption>(options);

            SelectedEngagement = string.IsNullOrWhiteSpace(previousSelectionId)
                ? Engagements.FirstOrDefault()
                : Engagements.FirstOrDefault(e => e.EngagementId == previousSelectionId) ?? Engagements.FirstOrDefault();
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

        partial void OnSelectedEngagementChanged(EngagementFilterOption? value)
        {
            Messenger.Send(new ValueChangedMessage<(string? EngagementId, string? EngagementName)>((value?.EngagementId, value?.Description)));
        }
    }

    public class EngagementFilterOption
    {
        public EngagementFilterOption(string engagementId, string description)
        {
            EngagementId = engagementId;
            Description = description;
        }

        public string EngagementId { get; }

        public string Description { get; }

        public override string ToString() => Description;
    }
}
